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

        iDClass rep; // um REP na rede para fazer a extração e junção de templates
        string[] templates;

        public MainWindow()
        {
            InitializeComponent();

            // meus componentes
            leitor = new Futronic();
            timer = new DispatcherTimer();
            rep = new iDClass();
            templates = new string[3]; // templates dos 3 dedos
        }

        private void Window_Load(object sender, RoutedEventArgs e)
        {
            dedo = 1;
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timer_Tick;
            timer.Start();

            rep.Login("192.168.0.19");
            txtEquip.Text = rep.Status;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            timer.Stop();
            if (leitor.Connected)
                leitor.Dispose();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            DateTime dt = DateTime.Now;
            try
            {
                if (leitor.Connected)
                {
                    if (leitor.IsFinger())
                    {
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
                            double t1 = DateTime.Now.Subtract(dt).TotalMilliseconds;
                            txtLeitor.Text = string.Format("Captura da digital {0} - Tempo de obtenção: {1:0.0}ms", dedo, t1);

                            // Essa parte por ser remota costuma ser lenta
                            int qualidade;
                            templates[dedo - 1] = rep.ExtractTemplate(bmp, out qualidade);
                            double t2 = DateTime.Now.Subtract(dt).TotalMilliseconds;

                            txtEquip.Text = string.Format("Qualidade: {0}% - Tempo de transmissão: {1:0.0}ms", qualidade, t2 - t1);

                            // vai para o proximo dedo
                            dedo++;
                            if (dedo > 3)
                                dedo = 1;
                        }
                    }
                    //else
                    //    txtLeitor.Text = "Coloque o dedo para a imagem " + dedo;
                }
                else
                {
                    if (leitor.Init())
                        txtLeitor.Text = "Leitor Futronic FS-80 reconhecido";
                    else
                        txtLeitor.Text = "Conecte o leitor Futronic FS-80 !";
                }
            }
            catch (Exception ex)
            {
                txtLeitor.Text = ex.Message;
            }
        }
    }
}