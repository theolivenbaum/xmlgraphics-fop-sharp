// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements.  See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to You under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License.  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Fop.Layout;

/// <summary>
/// Turns a URI an FO document refers to into bytes. The port of
/// <c>org.apache.xmlgraphics.io.ResourceResolver</c>, which FOP's
/// <c>InternalResourceResolver</c> delegates every external fetch to; only the read half is
/// modelled here, since the layout engine never writes.
/// <para>
/// Without one, an <c>fo:external-graphic</c> or a <c>background-image</c> can only name a file on
/// the local disk. That is the wrong assumption for a document whose illustrations live in a
/// content management system, an object store, a zip or a database: the caller would have to write
/// every image out to a temporary file first, purely so the engine could read it back.
/// </para>
/// <para>
/// A resolver is consulted for every image URI before the file system is. Return <c>null</c> for a
/// URI you do not have and the engine falls back to its normal handling, so a resolver covering one
/// scheme need not know about any other:
/// </para>
/// <code>
/// var processor = new FopProcessor
/// {
///     ResourceResolver = ResourceResolvers.FromDelegate(
///         uri =&gt; uri.StartsWith("icn:") ? store.Open(uri[4..]) : null),
/// };
/// </code>
/// <para>
/// Resolved bytes are read once per layout and carried in the area tree, so an image repeated on
/// every page costs one call. Implementations must be thread-safe only if the same processor is
/// used concurrently.
/// </para>
/// </summary>
public interface IResourceResolver
{
    /// <summary>
    /// Opens <paramref name="uri"/>, or returns <c>null</c> when this resolver does not have it.
    /// The engine reads the stream to the end and disposes it.
    /// </summary>
    /// <param name="uri">
    /// The URI exactly as the document wrote it, with any <c>url(...)</c> wrapper already stripped.
    /// It came out of a document, so treat it as untrusted input.
    /// </param>
    /// <returns>A readable stream over the resource, or <c>null</c>.</returns>
    Stream? GetResource(string uri);
}

/// <summary>The resolvers worth shipping, and how to combine them.</summary>
public static class ResourceResolvers
{
    /// <summary>A resolver that has nothing. Every URI falls through to the default handling.</summary>
    public static IResourceResolver None { get; } = new NoResources();

    /// <summary>
    /// A resolver from a function -- the common case, since most callers already have one.
    /// </summary>
    public static IResourceResolver FromDelegate(Func<string, Stream?> open) =>
        new DelegateResources(open ?? throw new ArgumentNullException(nameof(open)));

    /// <summary>
    /// Files under one or more base directories, looked up by the URI's last path segment.
    /// <para>
    /// Only the last segment is used, so a document cannot walk out of the directories it was
    /// pointed at with a crafted <c>src</c>.
    /// </para>
    /// </summary>
    public static IResourceResolver Directory(params string[] directories) =>
        new FileResources(directories ?? throw new ArgumentNullException(nameof(directories)));

    /// <summary>
    /// The first resolver that has the URI wins. Nulls in the list are skipped, so optional layers
    /// can be composed without a null check on each.
    /// </summary>
    public static IResourceResolver Compose(params IResourceResolver?[] resolvers) =>
        new CompositeResources([.. (resolvers ?? []).Where(r => r is not null).Select(r => r!)]);

    private sealed class NoResources : IResourceResolver
    {
        public Stream? GetResource(string uri) => null;
    }

    private sealed class DelegateResources(Func<string, Stream?> open) : IResourceResolver
    {
        public Stream? GetResource(string uri) => open(uri);
    }

    private sealed class CompositeResources(IReadOnlyList<IResourceResolver> resolvers) : IResourceResolver
    {
        public Stream? GetResource(string uri)
        {
            foreach (IResourceResolver resolver in resolvers)
            {
                Stream? stream = resolver.GetResource(uri);
                if (stream is not null)
                {
                    return stream;
                }
            }

            return null;
        }
    }

    private sealed class FileResources : IResourceResolver
    {
        private readonly string[] directories;

        internal FileResources(IEnumerable<string> dirs) =>
            directories = [.. dirs.Where(d => !string.IsNullOrWhiteSpace(d))];

        public Stream? GetResource(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            string leaf = Path.GetFileName(uri);
            if (leaf.Length == 0)
            {
                return null;
            }

            foreach (string directory in directories)
            {
                string candidate = Path.Combine(directory, leaf);
                if (File.Exists(candidate))
                {
                    return File.OpenRead(candidate);
                }
            }

            return null;
        }
    }
}
