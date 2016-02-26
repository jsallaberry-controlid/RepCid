using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace TestFutronic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Futronic leitor; // contrala a API leitor Frutronic
        DispatcherTimer timer; // timer que busca novos eventos a cada 1 segundo
        int dedo; // contado da posição do dedo

        public MainWindow()
        {
            InitializeComponent();

            // meus componentes
            leitor = new Futronic();
            timer = new DispatcherTimer();
        }

        private void Window_Load(object sender, RoutedEventArgs e)
        {
            dedo = 1;
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timer_Tick;
            timer.Start();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            timer.Stop();
            if (leitor.Connected)
                leitor.Dispose();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (leitor.Connected)
                {
                    if (leitor.IsFinger())
                    {
                        txtStatus.Text = "Dedo " + dedo;
                        using (Bitmap bmp = leitor.ExportBitMap())
                        {
                            switch (dedo)
                            {
                                case 1:
                                    dedo1.Source = bmp.ToBitmapSource();
                                    break;
                                case 2:
                                    dedo2.Source = bmp.ToBitmapSource();
                                    break;
                                case 3:
                                    dedo3.Source = bmp.ToBitmapSource();
                                    break;
                            }
                            dedo++;
                            if (dedo > 3)
                                dedo = 1;
                        }
                    }
                    else
                        txtStatus.Text = "Coloque o dedo para a imagem " + dedo;
                }
                else
                {
                    if (leitor.Init())
                        txtStatus.Text = "Leitor Futronic FS-80 reconhecido";
                    else
                        txtStatus.Text = "Conecte o leitor Futronic FS-80 !";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
        }
        
    }
}