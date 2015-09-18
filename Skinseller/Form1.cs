using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace Skinseller
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// Classes
        /// </summary>
        private Functions    Func = new Functions();


        /// <summary>
        /// Variables
        /// </summary>
        private Bitmap       _iBackground;
        private JObject      _jInventoryJson;
        private string       _sCurrentUser;
        private List<Bitmap> _lbItems = new List<Bitmap>();
        private Item         _cItem;


        /// <summary>
        /// Classes
        /// </summary>
        class Item
        {
            public int gameId { get; set; }
            public int contextId { get; set; }
            public long itemId { get; set; }
            public long classId { get; set; }
            public string itemName { get; set; }
            public string itemExt { get; set; }
            public string itemColor { get; set; }
            public string itemPrice { get; set; }
            public Bitmap itemImage { get; set; }
        }


        /// <summary>
        /// Dll impors
        /// </summary>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);


        /// <summary>
        /// Convert to long
        /// </summary>
        /// <param name="lowPart"></param>
        /// <param name="highPart"></param>
        /// <returns></returns>
        public int MakeLong(short lowPart, short highPart)
        {
            return (int)(((ushort)lowPart) | (uint)(highPart << 16));
        }


        /// <summary>
        /// Edits a listview control spacing
        /// </summary>
        /// <param name="listview">ListView to edit</param>
        /// <param name="cx">X axis</param>
        /// <param name="cy">Y axis</param>
        public void ListView_SetSpacing(ListView listview, short cx, short cy)
        {
            const int LVM_FIRST = 0x1000;
            const int LVM_SETICONSPACING = LVM_FIRST + 53;
            SendMessage(listview.Handle, LVM_SETICONSPACING,
            IntPtr.Zero, (IntPtr)MakeLong(cx, cy));
        }


        /// <summary>
        /// Form initializer
        /// </summary>
        public Form1()
        {
            InitializeComponent();

            /*Load temp image*/
            pic_Background.Image = Properties.Resources.DefaultBackground;
            _iBackground = Properties.Resources.DefaultBackground;
        }


        /// <summary>
        /// Form load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            /*Set listview spacing for better look and navigation*/
            ListView_SetSpacing(itemListView, 90, 90);
        }


        /// <summary>
        /// When user drags an item over the drop area
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            /*If it's html data, display drop icon*/
            if (e.Data.GetDataPresent(DataFormats.Html)) e.Effect = DragDropEffects.Copy;
        }


        /// <summary>
        /// When user drops an item over the drop area
        /// We only accept HTML drop
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DropZone_DragDrop(object sender, DragEventArgs e)
        {
            /*Get dropped html data and strip it down*/
            string Data = Func.ExtractHtmlFragmentFromClipboardData(e.Data.GetData(DataFormats.Html).ToString());

            /*Make sure it's the right kind of link*/
            if (Data.Contains("inventory/#"))
            {
                /*We only care about the item data, and not the html/full link*/
                string ItemData = Func.GetStringBetween(Data, "inventory/#", "\">");
                string ItemLink = Func.GetStringBetween(Data, "steamcommunity.com/", "/inventory");

                /*Split Data - Format: 730_2_123123123*/
                string[] DataArray = ItemData.Split('_');

                /*Set data*/
                Item MyItem      = new Item();
                MyItem.gameId    = Convert.ToInt16(DataArray[0]);
                MyItem.contextId = Convert.ToInt16(DataArray[1]);
                MyItem.itemId    = Convert.ToInt64(DataArray[2]);

                /*Query item token only if we havn't done so before*/
                /*Only need to load the inventory once - and we're gonna assume inventory will not change during process*/
                if(_jInventoryJson == null || _sCurrentUser != ItemLink)
                {
                    /*Load up inventory of user*/
                    _jInventoryJson = JObject.Parse(Func.Fetch(string.Format("http://steamcommunity.com/{0}/inventory/json/{1}/{2}", ItemLink, MyItem.gameId, MyItem.contextId)));
                    _sCurrentUser   = ItemLink;
                }

                /*Find the item*/
                foreach(var item in _jInventoryJson["rgInventory"])
                {
                    /*If the ID matches out item*/
                    if((long)item.Last.SelectToken("id") == MyItem.itemId)
                    {
                        /*Set the class id because we need it to search for Item Description and to get the Image*/
                        MyItem.classId = (long)item.Last.SelectToken("classid");

                        /*Go through all item descriptions*/
                        foreach(var desc in _jInventoryJson["rgDescriptions"])
                        {
                            /*If current description matches our classid*/
                            if((long)desc.Last.SelectToken("classid") == MyItem.classId)
                            {
                                /*Assign all values for our item*/
                                MyItem.itemName  = (string)desc.Last.SelectToken("market_name");
                                MyItem.itemExt   = Func.GetStringBetween(MyItem.itemName, "(", ")");
                                MyItem.itemColor = (string)desc.Last.SelectToken("name_color");
                                MyItem.itemImage = Func.BitmapFromUrl(string.Format("http://steamcommunity-a.akamaihd.net/economy/image/class/{0}/{1}/85fx85f", MyItem.gameId, MyItem.classId));
                                break;
                            }
                        }
                    }
                }

                /*Get item price from Steam market*/
                string priceJson = Func.Fetch(string.Format("http://steamcommunity.com/market/priceoverview/?country=NL&currency=3&appid=730&market_hash_name={0}", MyItem.itemName));

                /*If valid json*/
                try
                {
                    JObject priceObj = JObject.Parse(priceJson);

                    /*If item has a price on market - won't have price for high-tier*/
                    if (priceObj.SelectToken("lowest_price") != null)
                    {
                        MyItem.itemPrice    = ((string)priceObj.SelectToken("lowest_price")).Replace("€", "").Replace("--", "00");
                        string _googlePrice = Func.Fetch(string.Format("https://www.google.com/finance/converter?a={0}&from={1}&to={2}", MyItem.itemPrice, text_CurrancyFrom.Text, text_CurrancyTo.Text));
                        _googlePrice        = Func.GetStringBetween(_googlePrice, "<span class=bld>", " " + text_CurrancyTo.Text).Replace(".", ",");
                        decimal tempPrice   = decimal.Parse(_googlePrice);
                        tempPrice           = tempPrice * Convert.ToDecimal(text_Percentage.Text) / 100;
                        MyItem.itemPrice    = Math.Round(tempPrice).ToString();
                    }
                }
                catch
                {
                    /*Usually happens because item value is too low, and we don't really care*/
                    MessageBox.Show("Something happened.\n\n"
                        + "• Steam API is down\n"
                        + "• You no longer have the item in your inventory\n"
                        + "• Inventory is not public\n"
                        + "• Item value is too low", "Error");
                    return;
                }

                /*Confirmation stuff etc*/
                itemImageList.Images.RemoveByKey(MyItem.itemId.ToString());
                editPanel.Visible = true;
                pic_EditImg.Image = MyItem.itemImage;
                text_Edit.Text    = MyItem.itemPrice;
                _cItem            = MyItem;

                /*Focus price box*/
                text_Edit.Focus();

                /*Give our application focus*/
                SetForegroundWindow(Handle);
            }
            else
            {
                /*Dropped an invalid link*/
                MessageBox.Show("Invalid drop.\nDrag and drop items from your Steam inventory.");
            }
        }


        /// <summary>
        /// Button to confirm the price
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Edit_Click(object sender, EventArgs e)
        {
            if(_cItem != null)
            {
                /*Construct item and add to list*/
                Bitmap NBit = Func.MakeBitmap(_iBackground, _cItem.itemImage, _cItem.itemName, _cItem.itemExt, text_Edit.Text + " " + text_CurrancyTo.Text, "#" + _cItem.itemColor);
                _lbItems.Add(NBit);

                /*Add to listview*/
                itemImageList.Images.Add(_cItem.itemId.ToString(), NBit);
                ListViewItem lvi = new ListViewItem();
                lvi.ImageKey = _cItem.itemId.ToString();
                itemListView.Items.Add(lvi);

                /*Set to null and close*/
                _cItem = null;
                editPanel.Visible = false;
            }
        }


        /// <summary>
        /// Button to cancel adding of item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Cancel_Click(object sender, EventArgs e)
        {
            /*Set to null and close*/
            _cItem = null;
            editPanel.Visible = false;
        }


        /// <summary>
        /// Change background of the item background
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pic_changeBG_Click(object sender, EventArgs e)
        {
            /*Open file dialog*/
            using (OpenFileDialog diag = new OpenFileDialog())
            {
                /*Set image filter and other stuff*/
                diag.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
                diag.InitialDirectory = Directory.GetCurrentDirectory();
                diag.Title = "Select an Item background (85x85)";

                /*Open the dialog and wait for user to hit OK*/
                if (diag.ShowDialog() == DialogResult.OK)
                {
                    /*Double check so the item that user selected exists*/
                    if (File.Exists(diag.FileName))
                    {
                        /*Load up the image selected*/
                        Bitmap checkImg = new Bitmap(diag.FileName);

                        /*If height or width is above 85 we'll resize image, but let user know that quality might be worsen*/
                        if (checkImg.Width > 85 || checkImg.Height > 85)
                        {
                            /*Alert user then resize the image*/
                            MessageBox.Show("Image Width/Height is higher than 85.\nImage will be resized to fit.\n(Might not look good)", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            checkImg = Func.resizeImage(checkImg, new Size(85, 85));
                        }

                        /*Assign selected image to global var*/
                        _iBackground = checkImg;
                        pic_Background.Image = checkImg;
                    }
                }
            }
        }


        /// <summary>
        /// Delete button from Menu to delete an item from List and Listview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuDeleteItem_Click(object sender, EventArgs e)
        {
            /*Make sure one or more items are selected, although we only care about first one*/
            if(itemListView.SelectedItems.Count > 0)
            {
                /*Get item index then delete it from the global list and then from listview*/
                var item = itemListView.SelectedItems[0];
                _lbItems.RemoveAt(item.Index);
                itemListView.Items.RemoveAt(item.Index);
            }
        }


        /// <summary>
        /// Save picture button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_SaveImg_Click(object sender, EventArgs e)
        {
            if (_lbItems.Count > 0)
            {
                /*Get number of rows needed to fit items*/
                int ImageRows = (int)Math.Ceiling((double)_lbItems.Count / (double)5);

                /*Calculate fit*/
                Bitmap FullImage = new Bitmap(547, (90 * ImageRows) + 5);
                using (Graphics g = Graphics.FromImage(FullImage))
                {
                    /*If we have hex bg set*/
                    string hexColor = text_BGHex.Text;
                    if (hexColor.StartsWith("#") && hexColor.Length == 7)
                    {
                        g.Clear(ColorTranslator.FromHtml(hexColor));
                    }

                    /*Default values*/
                    int width = 5;
                    int height = 5;
                    int index = 0;

                    /*Go through each item*/
                    for (int i = 0; i < _lbItems.Count; i++)
                    {
                        /*Draw image in*/
                        g.DrawImage(_lbItems[i], new Point(width, height));

                        /*Increase index and width for next image*/
                        index++;
                        width += 90;

                        /*If index is 6, that means we've drawn 6 items*/
                        /*Reset default values and increase height to start drawing on next row*/
                        if (index == 6)
                        {
                            height += 90;
                            index = 0;
                            width = 5; //we set this to 5 so we have a margin from left to make it look nicer
                        }
                    }
                }

                /*Save the image*/
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "Images|*.png;";
                sfd.DefaultExt = ".png";
                sfd.InitialDirectory = Directory.GetCurrentDirectory();
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    FullImage.Save(sfd.FileName, ImageFormat.Png);
                }
            }
            else
            {
                MessageBox.Show("There is nothing to save, dummy.", "Information");
            }
        }


        /// <summary>
        /// Make sure that the percentage entered is correct
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void text_Percentage_Leave(object sender, EventArgs e)
        {
            int Result;

            /*Attempt to parse the value*/
            if(!int.TryParse(text_Percentage.Text, out Result))
            {
                text_Percentage.Text = "70";
            }

            /*Check if it's an incorrect percentage value (eg. 0 or 101)*/
            if(Result <= 0 || Result > 100)
            {
                text_Percentage.Text = "70";
            }
        }


        /// <summary>
        /// Shows/Hides the menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_ShowMenu_Click(object sender, EventArgs e)
        {
            if(panel_Menu.Visible)
            {
                panel_Menu.Visible = false;
                btn_ShowMenu.Text = "► Show menu";
            }
            else
            {
                panel_Menu.Visible = true;
                btn_ShowMenu.Text = "▼ Show menu";
            }
        }
    }
}
