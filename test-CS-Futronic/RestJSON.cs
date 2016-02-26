using System;
using System.Reflection;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TestFutronic
{
    #region Estruturas de uso geral

    // Para objetos que precisam informar a sessão
    [DataContract]
    public abstract class SessionRequest
    {
        [DataMember(Name = "session")]
        public string Session { get; set; }
    }

    // string cURL = "https://192.168.0.146/template_extract.fcgi?session=" + rep.iDClassSession +"&width=" + digital.Width/3 + "&height=" + digital.Height;
    // {"quality":3,"template":"SUNSUzIxAAAAagEBAAAAAMUAxQBSAFkAAAAAgDwBJAAsAPcOCAAIAAgAACAFAQApASsFACkFdMaZBABGEonFwQUASRqMqQUASyCJowQAPS+DxcYAREIBAQAAABYAAAAAAgUAAAAAAABFQg=="}
    [DataContract]
    public class TemplateResult : StatusResult
    {
        [DataMember(Name = "quality")]
        public int Qualidate;

        [DataMember(Name = "template")]
        public string Template;
    }

    #endregion 

    // REST Services JSON https://msdn.microsoft.com/en-us/library/hh674188.aspx
    // .Net 4.5: JSON https://msdn.microsoft.com/pt-br/library/windows/apps/xaml/hh770289.aspx
    public class RestJSON
    {
        /// <summary>
        /// Encoder default do REP
        /// </summary>
        public static readonly Encoding DefaultEncode = Encoding.GetEncoding("ISO-8859-1");

        // obrigatório para configurar o 'ServicePointManager' no primeiro uso
        static RestJSON()
        {
            // para não quebrar nenhum pacote
            ServicePointManager.Expect100Continue = false;

            // Para autorizar qualquer certificado SSL
            // http://stackoverflow.com/questions/18454292/system-net-certificatepolicy-to-servercertificatevalidationcallback-accept-all-c
            ServicePointManager.ServerCertificateValidationCallback = (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => { return true; };

            // Permite o uso de SSL auto assinados
            SetAllowUnsafeHeaderParsing20();
        }

        // https://o2platform.wordpress.com/2010/10/20/dealing-with-the-server-committed-a-protocol-violation-sectionresponsestatusline/
        public static bool SetAllowUnsafeHeaderParsing20()
        {
            //Get the assembly that contains the internal class
            Assembly aNetAssembly = Assembly.GetAssembly(typeof(System.Net.Configuration.SettingsSection));
            if (aNetAssembly != null)
            {
                //Use the assembly in order to get the internal type for the internal class
                Type aSettingsType = aNetAssembly.GetType("System.Net.Configuration.SettingsSectionInternal");
                if (aSettingsType != null)
                {
                    //Use the internal static property to get an instance of the internal settings class.
                    //If the static instance isn't created allready the property will create it for us.
                    object anInstance = aSettingsType.InvokeMember("Section",
                    BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic, null, null, new object[] { });
                    if (anInstance != null)
                    {
                        //Locate the private bool field that tells the framework is unsafe header parsing should be allowed or not
                        FieldInfo aUseUnsafeHeaderParsing = aSettingsType.GetField("useUnsafeHeaderParsing", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (aUseUnsafeHeaderParsing != null)
                        {
                            aUseUnsafeHeaderParsing.SetValue(anInstance, true);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Faz uma chamada JSON ao IP especificado serializando um objeto, e devolvendo outro do tipo esperado
        /// </summary>
        public static T SendJson<T>(string cIP, object objSend, string session = "")
        {
            Type tpResult = typeof(T);
            object result;
            try
            {
                string cURL = "https://" + cIP + "/";
                string cCMD;
                Type tpSend = objSend.GetType();
                if (tpSend == typeof(string))
                {
                    // Obtem o nome da chamada real
                    cURL += objSend + ".fcgi";
                    if (string.IsNullOrEmpty(session))
                        cCMD = "{}";
                    else
                        cCMD = "{\"session\": \"" + session + "\"}";
                }
                else
                {
                    DataContractAttribute dca = (DataContractAttribute)Attribute.GetCustomAttribute(tpSend, typeof(DataContractAttribute));
                    cURL += dca.Name + ".fcgi";

                    if (!string.IsNullOrEmpty(session) && tpSend.IsSubclassOf(typeof(SessionRequest)))
                        ((SessionRequest)objSend).Session = session;

                    // Serializa o Objeto em formato JSON em um buffer em memória
                    using (MemoryStream ms = new MemoryStream())
                    {
                        DataContractJsonSerializer jsonConnect = new DataContractJsonSerializer(tpSend);
                        jsonConnect.WriteObject(ms, objSend);

                        ms.Position = 0;
                        using (StreamReader sr = new StreamReader(ms))
                            cCMD = sr.ReadToEnd();
                    }
                }

                // cCMD = "{login:\"admin\",password:\"admin\"}";
                // Faz a chamada ao Serviço
                WebRequest req = WebRequest.Create(cURL);
                req.ContentType = "application/json";
                req.Method = "POST";

                using (StreamWriter stw = new StreamWriter(req.GetRequestStream()))
                    // Lê o buffer para uma string e envia ao serviço via POST
                    stw.Write(cCMD);

                // Obtem a resposta e deserializa o objeto
                using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                    result = new DataContractJsonSerializer(tpResult).ReadObject(response.GetResponseStream());

                if (tpResult == typeof(StatusResult) || tpResult.IsSubclassOf(typeof(StatusResult)))
                {
                    if (((StatusResult)result).Codigo == 0 && ((StatusResult)result).Status == null)
                    {
                        ((StatusResult)result).Codigo = 200;
                        ((StatusResult)result).Status = "OK";
                    }
                }
            }
            catch (Exception ex)
            {
                WebException wex = null;
                if (ex is WebException)
                    wex = (WebException)ex;
                else if (ex.InnerException != null && ex.InnerException is WebException)
                    wex = (WebException)ex.InnerException;

                if (wex != null)
                {
                    if (wex.Response != null)
                    {
                        HttpWebResponse response = (HttpWebResponse)wex.Response;
                        string cReceive = null;
                        using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                            cReceive = sr.ReadToEnd();

                        if (cReceive.Contains("{")) // provavel JSON!
                        {
                            try
                            {
                                DataContractJsonSerializer deserializer = new DataContractJsonSerializer(tpResult);
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    byte[] bt = UTF8Encoding.UTF8.GetBytes(cReceive);
                                    ms.Write(bt, 0, bt.Length);
                                    ms.Position = 0;
                                    result = deserializer.ReadObject(ms);
                                }
                            }
                            catch (Exception)
                            {
                                throw new Exception("ERRO JSON: " + cReceive, ex);
                            }
                        }
                        else
                            throw new Exception("ERRO REQUEST: " + cReceive, ex);
                    }
                    else
                        throw wex;
                }
                else
                {
                    throw new Exception("ERRO: ", ex);
                }
            }
            return (T)result;
        }

        /// <summary>
        /// Obtem um template de uma imagem serializada, usando um IDClass pelo IP
        /// </summary>
        public static byte[] RequestTemplate(string cIP, byte[] bt, int width, int height, string session, out int LastQuality)
        {
            string cURL = "https://" + cIP + "/template_extract.fcgi?session=" + session + "&width=" + width + "&height=" + height;
            WebRequest req = WebRequest.Create(cURL);
            req.ContentType = "application/octet-stream";
            req.Method = "POST";

            Stream stw = req.GetRequestStream();
            stw.Write(bt, 0, bt.Length);

            TemplateResult tpResult;
            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            {
                DataContractJsonSerializer jsonResult = new DataContractJsonSerializer(typeof(TemplateResult));
                tpResult = (TemplateResult)jsonResult.ReadObject(response.GetResponseStream());
                LastQuality = tpResult.Qualidate;
            }
            return Convert.FromBase64String(tpResult.Template);
        }

        /// <summary>
        /// Obtem os bytes monocromaticos de uma imagem
        /// </summary>
        public static byte[] GetBytes(Bitmap digital)
        {
            List<Byte> bt = new List<byte>();
            for (int y = 0; y < digital.Height; y++)
                for (int x = 0; x < digital.Width; x++)
                    bt.Add(digital.GetPixel(x, y).G);

            return bt.ToArray();
        }
    }
}