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

using System.Text;

namespace Fop.Render.Text;

/// <summary>
/// A <see cref="TextWriter"/> decorator that defers trailing whitespace: any run of whitespace is
/// held back and forwarded to the inner writer only once non-whitespace follows. This lets the
/// text back-ends write straight to the caller's stream while still reproducing their
/// <c>builder.ToString().TrimEnd() + "\n"</c> normalisation -- interior whitespace is preserved,
/// but the document's trailing whitespace is dropped and replaced by a single newline via
/// <see cref="FinishWithNewline"/>.
/// </summary>
internal sealed class TrailingWhitespaceTrimmer(TextWriter inner) : TextWriter
{
    // Buffers the most recent run of whitespace until we know whether non-whitespace follows it.
    private readonly StringBuilder pending = new();

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        if (char.IsWhiteSpace(value))
        {
            pending.Append(value);
            return;
        }

        FlushPending();
        inner.Write(value);
    }

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (char c in value)
        {
            Write(c);
        }
    }

    /// <summary>Discards the buffered trailing whitespace and writes a single terminating newline.</summary>
    public void FinishWithNewline()
    {
        pending.Clear();
        inner.Write('\n');
    }

    private void FlushPending()
    {
        if (pending.Length > 0)
        {
            inner.Write(pending.ToString());
            pending.Clear();
        }
    }
}
