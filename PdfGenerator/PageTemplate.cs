﻿using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PdfGenerator
{
    public class PageTemplate
    {
        public XUnit Width { get; set; }
        public XUnit Height { get; set; }

        public XPath ContextPath { get; set; }

        public IList<Element> Elements { get; set; } = new List<Element>();

        public PdfDocument GetDocuments(XDocument document)
        {
            return GetDocuments(document.XPathSelectElements(this.ContextPath.Path));
        }
        private PdfDocument GetDocuments(IEnumerable<XElement> elements)
        {
            var document = new PdfDocument
            {
                PageLayout = PdfPageLayout.SinglePage
            };
            foreach (var context in elements)
            {
                var page = document.AddPage();
                page.Width = this.Width;
                page.Height = this.Height;

                // Get an XGraphics object for drawing
                using (var gfx = XGraphics.FromPdfPage(page))
                {
                    // Create a font

                    foreach (var item in this.Elements.OrderByDescending(x => x.ZIndex))
                    {
                        if (!item.IsVisible.GetValue(context))
                            continue;

                        if (item is TextElement textElement)
                        {
                            var position = textElement.Position.GetValue(context);
                            var zIndex = textElement.ZIndex.GetValue(context);

                            var frame = textElement.Position.GetValue(context);
                            var startPosition = frame.Location;
                            var currentPosition = startPosition;

                            foreach (var paragraph in textElement.Paragraphs)
                            {
                                if (!paragraph.IsVisible.GetValue(context))
                                    continue;

                                var lines = new List<List<(string textToPrint, XFont font, XBrush brush, XPoint printPosition, XLineAlignment alignment, XSize size)>>();
                                var currentLine = new List<(string textToPrint, XFont font, XBrush brush, XPoint printPosition, XLineAlignment alignment, XSize size)>();
                                currentPosition = new XPoint(currentPosition.X, currentPosition.Y + paragraph.BeforeParagraph.GetValue(context));
                                foreach (var run in paragraph.Runs)
                                {
                                    if (!run.IsVisible.GetValue(context))
                                        continue;

                                    lines.Add(currentLine);

                                    var font = new XFont(run.FontName.Value.GetValue(context), run.EmSize.Value.GetValue(context), run.FontStyle.Value.GetValue(context));
                                    var height = font.Height * paragraph.Linespacing.GetValue(context);

                                    if (run is LineBreakRun)
                                    {
                                        currentPosition = new XPoint(startPosition.X, currentPosition.Y + height);
                                    }
                                    else if (run is TextRun textRun)
                                    {


                                        var textForRun = textRun.Text.GetValue(context);
                                        if (string.IsNullOrEmpty(textForRun))
                                            continue;

                                        var wordSizes = textForRun.Split(' ').Select(x => new { Size = gfx.MeasureString(x, font), Word = x }).ToArray();
                                        var spaceSize = gfx.MeasureString(" ", font);

                                        int wordsToPrint;
                                        for (int i = 0; i < wordSizes.Length; i += wordsToPrint)
                                        {
                                            double lineWidth = 0;
                                            bool endOfLine = false;
                                            for (wordsToPrint = 0; wordsToPrint + i < wordSizes.Length; wordsToPrint++)
                                            {
                                                var w = wordSizes[i + wordsToPrint];

                                                if (w.Size.Width + currentPosition.X + lineWidth > position.Right && i + wordsToPrint != 0 /*we can't make a linebreka before the first word*/)
                                                {
                                                    // we are over the bounding. set current Position to next line
                                                    endOfLine = true;
                                                    break;
                                                }
                                                lineWidth += w.Size.Width + (wordsToPrint == 0 ? 0 : spaceSize.Width);
                                            }
                                            wordsToPrint = Math.Max(wordsToPrint, 1); // we want to print at least one word other wise we will not consume it and will print nothing agiain

                                            var textToPrint = string.Join(" ", wordSizes.Skip(i).Take(wordsToPrint).Select(x => x.Word));

                                            currentLine.Add((textToPrint, font, XBrushes.Black, currentPosition, paragraph.Alignment.GetValue(context), gfx.MeasureString(textToPrint, font)));
                                            currentPosition = new XPoint(currentPosition.X + spaceSize.Width + lineWidth, currentPosition.Y);

                                            if (endOfLine)
                                            {
                                                currentPosition = new XPoint(startPosition.X, currentPosition.Y + height);
                                                currentLine = new List<(string textToPrint, XFont font, XBrush brush, XPoint currentPosition, XLineAlignment alignment, XSize size)>();
                                                lines.Add(currentLine);
                                            }

                                            if (wordSizes.Skip(i).Take(wordsToPrint).Max(x => x.Size.Height) + currentPosition.Y > position.Bottom)
                                                goto END; // reached end of box


                                        }
                                    }
                                }

                                // now we calculate LineAlignment and print 
                                END:;
                                var maximumWidth = textElement.Position.GetValue(context).Width;
                                foreach (var line in lines.Where(x => x.Count > 0))
                                {
                                    var leftmost = line.Min(x => x.printPosition.X);
                                    var rightmost = line.Max(x => x.printPosition.X + x.size.Width);
                                    var width = rightmost - leftmost;

                                    double offset;
                                    switch (paragraph.Alignment.GetValue(context))
                                    {
                                        case XLineAlignment.Near:
                                            offset = 0;
                                            break;
                                        case XLineAlignment.Center:
                                            offset = (maximumWidth - width) / 2;
                                            break;
                                        case XLineAlignment.Far:
                                            offset = maximumWidth - width;
                                            break;
                                        case XLineAlignment.BaseLine:
                                            throw new NotSupportedException("Not sure what this means");
                                        default:
                                            throw new NotSupportedException($"The Alignment {paragraph.Alignment} is not supported.");
                                    }
                                    foreach (var print in line)
                                        gfx.DrawString(print.textToPrint, print.font, print.brush, new XPoint(print.printPosition.X + offset, print.printPosition.Y), XStringFormats.TopLeft);
                                }
                                currentPosition = new XPoint(startPosition.X, currentPosition.Y + lines.Where(x => x.Count > 0).Last().Max(x => x.size.Height) + paragraph.AfterParagraph.GetValue(context));

                            }





                            //gfx.MeasureString(,, XStringFormats.BottomRight)
                        }
                        else if (item is ImageElement imageElement)
                        {

                        }
                        else
                            throw new NotSupportedException($"Element of Type {item?.GetType()} is not supported.");


                    }

                    //// Draw the text
                    //gfx.DrawString("Hello, World!", font, XBrushes.Black,
                    //  new XRect(0, 0, page.Width, page.Height),
                    //  XStringFormats.Center);

                }

            }
            return document;
        }
    }
}