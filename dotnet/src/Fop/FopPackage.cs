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

namespace Fop;

/// <summary>
/// Marker for the <c>Fop</c> NuGet package. This package bundles the full C# port
/// of Apache FOP; the component assemblies (<c>Fop.Util</c>, <c>Fop.Fo</c>,
/// <c>Fop.Layout</c>, <c>Fop.Render.Pdf</c>, <c>Fop.Render.Text</c>, ...) ship
/// alongside it in <c>lib</c>.
/// <para>
/// The high-level entry point is <c>Fop.Render.Pdf.FopProcessor</c> (FO in, PDF out).
/// Plain-text/Markdown/HTML output is available through the <c>Fop.Render.Text</c>
/// renderers.
/// </para>
/// </summary>
public static class FopPackage
{
    /// <summary>The published package identifier.</summary>
    public const string PackageId = "Fop";
}
