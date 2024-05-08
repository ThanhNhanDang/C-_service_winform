using AppWinform_main.Database;
using AppWinform_main.Entity;
using AppWinform_main.Service;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppWinform_main
{
    public partial class FormSaveImage : Form
    {
        private int width = 468;
        private int height = 318;
        private string _pathSave = Application.StartupPath + @"Images\User";
        private string source = "";
        private TagInfoService _tagInfoService = new TagInfoService();
        public FormSaveImage()
        {
            InitializeComponent();
        }
        #region Add User

        #region Resize Image
        static Image FixedSize(Image imgPhoto, int Width, int Height)
        {
            int sourceWidth = imgPhoto.Width;
            int sourceHeight = imgPhoto.Height;
            int sourceX = 0;
            int sourceY = 0;
            int destX = 0;
            int destY = 0;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)Width / (float)sourceWidth);
            nPercentH = ((float)Height / (float)sourceHeight);
            if (nPercentH < nPercentW)
            {
                nPercent = nPercentH;
                destX = System.Convert.ToInt16((Width -
                              (sourceWidth * nPercent)) / 2);
            }
            else
            {
                nPercent = nPercentW;
                destY = System.Convert.ToInt16((Height -
                              (sourceHeight * nPercent)) / 2);
            }

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap bmPhoto = new Bitmap(Width, Height,
                              PixelFormat.Format24bppRgb);
            bmPhoto.SetResolution(imgPhoto.HorizontalResolution,
                             imgPhoto.VerticalResolution);

            Graphics grPhoto = Graphics.FromImage(bmPhoto);
            grPhoto.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;

            grPhoto.DrawImage(imgPhoto,
                new Rectangle(destX, destY, destWidth, destHeight),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return bmPhoto;
        }
        #endregion


        #region Save Image

        /* private void SavaImage()
         {
             var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
             if (dataPackageView.Contains(StandardDataFormats.Bitmap))
             {
                 IRandomAccessStreamReference imageReceived = null;
                 try
                 {
                     imageReceived = await dataPackageView.GetBitmapAsync();
                 }
                 catch (Exception ex)
                 {
                 }
             }
         }*/
        #endregion

        #endregion


        #region Btn Event
        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Open Image";
                dlg.Filter = "Image Files|*.jpg;*.jpeg;*.png;";

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    // Create a new Bitmap object from the picture file on disk,
                    // and assign that to the PictureBox.Image property
                    source = dlg.FileName;
                    Image image = FixedSize(new Bitmap(source), width, height);
                    if (ptbVao1.Image != null)
                    {
                        ptbVao1.Image.Dispose();
                    }
                    ptbVao1.Image = image;
                }
            }
        }
        #endregion

        private async void button2_Click(object sender, EventArgs e)
        {
            if (tbTid.Text == "")
            {
                return;
            }
            int selectedIndex = cbField.SelectedIndex;
            string field = "";
            string dest;
            if (selectedIndex == 0)
                field = "imgXePath";
            else if (selectedIndex == 1)
                field = "imgBienSoPath";
            else if (selectedIndex == 2)
                field = "imgNgPath";
            dest = _pathSave + $@"\{tbTid.Text}";
            if (!System.IO.Directory.Exists(dest))
            {
                System.IO.Directory.CreateDirectory(dest);
            }
            ptbVao1.Image.Save(dest + $@"\{tbTid.Text}{field}.png", ImageFormat.Png);
            await _tagInfoService.UpdateByKey(field, $@"\{tbTid.Text}\{tbTid.Text}{field}.png", "tidNg", tbTid.Text);
            MessageBox.Show("Thành Công!");
        }

        private void FormSaveImage_Load(object sender, EventArgs e)
        {
            cbField.SelectedIndex = 0;
        }
    }
}
