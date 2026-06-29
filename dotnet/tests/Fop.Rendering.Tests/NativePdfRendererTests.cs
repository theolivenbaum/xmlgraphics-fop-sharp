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

using System;
using System.IO;
using System.Text;
using Fop.Render.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Fop.Rendering.Tests;

/// <summary>
/// End-to-end tests for the native (PdfSharp-free) PDF renderer. Beyond a structural check, each PDF
/// is re-opened with PdfSharp's reader to confirm an independent parser accepts the file (valid xref,
/// trailer and page tree).
/// </summary>
public class NativePdfRendererTests
{
    private const string OnePage = """
        <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format" font-family="Helvetica" font-size="12pt">
          <fo:layout-master-set>
            <fo:simple-page-master master-name="A4" page-width="210mm" page-height="297mm"
                margin="20mm">
              <fo:region-body/>
            </fo:simple-page-master>
          </fo:layout-master-set>
          <fo:page-sequence master-reference="A4">
            <fo:flow flow-name="xsl-region-body">
              <fo:block text-decoration="underline" letter-spacing="1pt">Native heading</fo:block>
              <fo:block>Body text with <fo:basic-link external-destination="https://example.org">a link</fo:basic-link>.</fo:block>
              <fo:block>
                <fo:instream-foreign-object content-width="80pt" content-height="80pt">
                  <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80" viewBox="0 0 80 80">
                    <circle cx="40" cy="40" r="30" fill="orange" stroke="black" stroke-width="2"/>
                  </svg>
                </fo:instream-foreign-object>
              </fo:block>
            </fo:flow>
          </fo:page-sequence>
        </fo:root>
        """;

    [Fact]
    public void ProducesWellFormedPdf()
    {
        byte[] pdf = new FopProcessor().ConvertNative(OnePage);

        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        string tail = Encoding.ASCII.GetString(pdf, Math.Max(0, pdf.Length - 8), Math.Min(8, pdf.Length));
        Assert.Contains("EOF", tail);
    }

    [Fact]
    public void PdfSharpCanReopenNativeOutput()
    {
        byte[] pdf = new FopProcessor().ConvertNative(OnePage);
        using var input = new MemoryStream(pdf);
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public void WritesToNonSeekableStream()
    {
        // The native writer serializes in a single forward pass (xref offsets tracked by a running
        // byte counter), so it must not require a seekable output stream.
        using var foInput = new MemoryStream(Encoding.UTF8.GetBytes(OnePage));
        using var backing = new MemoryStream();
        using (var output = new WriteOnlyForwardStream(backing))
        {
            new FopProcessor().ConvertNative(foInput, output);
        }

        byte[] pdf = backing.ToArray();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        using var reopen = new MemoryStream(pdf);
        using var doc = PdfReader.Open(reopen, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public void MultiPageDocumentReportsCorrectPageCount()
    {
        var blocks = new StringBuilder();
        for (int i = 0; i < 80; i++)
        {
            blocks.Append($"<fo:block>line {i}</fo:block>");
        }

        string fo = $"""
            <fo:root xmlns:fo="http://www.w3.org/1999/XSL/Format" font-size="12pt">
              <fo:layout-master-set>
                <fo:simple-page-master master-name="p" page-width="200pt" page-height="120pt">
                  <fo:region-body/>
                </fo:simple-page-master>
              </fo:layout-master-set>
              <fo:page-sequence master-reference="p">
                <fo:flow flow-name="xsl-region-body">{blocks}</fo:flow>
              </fo:page-sequence>
            </fo:root>
            """;

        byte[] pdf = new FopProcessor().ConvertNative(fo);
        using var input = new MemoryStream(pdf);
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        Assert.True(doc.PageCount > 1, $"expected multiple pages, got {doc.PageCount}");
    }

    /// <summary>A forward-only, non-seekable stream that forwards writes to a backing stream.</summary>
    private sealed class WriteOnlyForwardStream(Stream backing) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => backing.Write(buffer, offset, count);

        public override void Flush() => backing.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
