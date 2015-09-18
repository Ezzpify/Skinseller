using System;
using System.Text;
using System.Drawing;
using System.Net;
using System.IO;

namespace Skinseller
{
    public class Functions
    {
        /// <summary>
        /// Extracts html from a HtmlFragment
        /// </summary>
        /// <param name="htmlDataString">Html string</param>
        /// <returns></returns>
        public string ExtractHtmlFragmentFromClipboardData(string htmlDataString)
        {
            // Byte count from the beginning of the clipboard to the start of the fragment.
            int startFragmentIndex = htmlDataString.IndexOf("StartFragment:");
            if (startFragmentIndex < 0)
            {
                return "ERROR: Unrecognized html header";
            }
            // TODO: We assume that indices represented by strictly 10 zeros ("0123456789".Length),
            // which could be wrong assumption. We need to implement more flrxible parsing here
            startFragmentIndex = Int32.Parse(htmlDataString.Substring(startFragmentIndex + "StartFragment:".Length, 10));
            if (startFragmentIndex < 0 || startFragmentIndex > htmlDataString.Length)
            {
                return "ERROR: Unrecognized html header";
            }

            // Byte count from the beginning of the clipboard to the end of the fragment.
            int endFragmentIndex = htmlDataString.IndexOf("EndFragment:");
            if (endFragmentIndex < 0)
            {
                return "ERROR: Unrecognized html header";
            }
            // TODO: We assume that indices represented by strictly 10 zeros ("0123456789".Length),
            // which could be wrong assumption. We need to implement more flrxible parsing here
            endFragmentIndex = Int32.Parse(htmlDataString.Substring(endFragmentIndex + "EndFragment:".Length, 10));
            if (endFragmentIndex > htmlDataString.Length)
            {
                endFragmentIndex = htmlDataString.Length;
            }

            // CF_HTML is entirely text format and uses the transformation format UTF-8
            byte[] bytes = Encoding.UTF8.GetBytes(htmlDataString);
            return Encoding.UTF8.GetString(bytes, startFragmentIndex, endFragmentIndex - startFragmentIndex);
        }


        /// <summary>
        /// Return one string inbetween two strings
        /// </summary>
        /// <param name="source">Source string</param>
        /// <param name="start">Start string</param>
        /// <param name="end">End string</param>
        /// <returns></returns>
        public string GetStringBetween(string source, string start, string end)
        {
            int startIndex = source.IndexOf(start);
            if (startIndex != -1)
            {
                int endIndex = source.IndexOf(end, startIndex + 1);
                if (endIndex != -1)
                {
                    return source.Substring(startIndex + start.Length, endIndex - startIndex - start.Length);
                }
            }
            return string.Empty;
        }


        /// <summary>
        /// Returns resized image
        /// </summary>
        /// <param name="imgToResize">Image to resize</param>
        /// <param name="size">Size of new image</param>
        /// <returns></returns>
        public Bitmap resizeImage(Bitmap imgToResize, Size size)
        {
            return (new Bitmap(imgToResize, size));
        }


        /// <summary>
        /// Return bitmap from Url
        /// </summary>
        /// <param name="url">Image web url</param>
        /// <returns></returns>
        public Bitmap BitmapFromUrl(string url)
        {
            var request = WebRequest.Create(url);
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                return (Bitmap)Image.FromStream(stream);
            }
        }


        /// <summary>
        /// Fetch html string from web url
        /// </summary>
        /// <param name="url">Web url</param>
        /// <returns></returns>
        public string Fetch(string url)
        {
            /*Using WebClient to quickly download data*/
            using (WebClient wc = new WebClient())
            {
                try
                {
                    /*Download data and return the string*/
                    wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    string HtmlResult = wc.DownloadString(url);
                    return HtmlResult;
                }
                catch (Exception ex)
                {
                    /*Something failed so return 0 for some reason*/
                    /*I didn't even add a check for this so let's pray it works :3*/
                    return "0";
                }
            }
        }


        /// <summary>
        /// Draws a new bitmap for the item
        /// </summary>
        /// <param name="ImageBG">Background image for the new Image</param>
        /// <param name="ImageItem">Item image</param>
        /// <param name="ItemName">Item name</param>
        /// <param name="ItemExterior">Item exterior (Field-tested etc)</param>
        /// <param name="ItemPrice">Item price (plus additional information such as currency €)</param>
        /// <param name="ItemColo">Item color in HEX</param>
        /// <returns></returns>
        public Bitmap MakeBitmap(Bitmap ImageBG, Bitmap ImageItem, string ItemName, string ItemExterior, string ItemPrice, string ItemColor)
        {
            /*Set format so text that we're drawing will be centered*/
            StringFormat format = new StringFormat();
            format.LineAlignment = StringAlignment.Center;
            format.Alignment = StringAlignment.Center;

            /*Initialize a new bitmap*/
            Bitmap NewBitmap      = new Bitmap(85, 85);

            /*Initialize stuff we'll need for painting*/
            FontFamily FontFam    = new FontFamily("Segoe UI");
            Font FontExt          = new Font(FontFam, 8, FontStyle.Regular);
            Font FontPrice        = new Font(FontFam, 8, FontStyle.Regular);
            SolidBrush BrushExt   = new SolidBrush(ColorTranslator.FromHtml(ItemColor));
            SolidBrush BrushPrice = new SolidBrush(Color.WhiteSmoke);

            /*For knives it will return an item color of Purple even though it's a ST, so we'll hard check that here*/
            if(ItemName.Contains("StatTrak"))
            {
                BrushExt = new SolidBrush(ColorTranslator.FromHtml("#CF6A32"));
            }

            /*Draw using Graphics*/
            using (Graphics g = Graphics.FromImage(NewBitmap))
            {
                /*First draw background image, then draw the item image*/
                g.DrawImage(ImageBG, new Rectangle(0, 0, ImageBG.Width, ImageBG.Height));
                g.DrawImage(ImageItem, new Point(2, 2));

                /*First draw Item Exterior name then draw the price at the bottom*/
                g.DrawString(ItemExterior, FontExt, BrushExt, new Rectangle(0, 0, 85, 17), format);
                g.DrawString(ItemPrice, FontPrice, BrushPrice, new Rectangle(0, 68, 85, 17), format);
            }

            /*Return the new bitmap*/
            return NewBitmap;
        }
    }
}
