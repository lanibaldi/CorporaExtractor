using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using TweetSharp;

namespace CorporaExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "-mongodb")
            {
                var connectionString = "mongodb://localhost";
                var client = new MongoClient(connectionString);
                var server = client.GetServer();
                try
                {
                    bool enableWordCloud = false;
                    string wordCloud = ConfigurationManager.AppSettings["WordCloud"];
                    if (!string.IsNullOrEmpty(wordCloud))
                        enableWordCloud = Convert.ToBoolean(wordCloud);

                    string collName = ConfigurationManager.AppSettings["CollectionName"];
                    if (string.IsNullOrEmpty(collName))
                        collName = "tweets";

                    var database = server.GetDatabase("tcat"); // "tcat" is the name 
                    if (!database.CollectionExists(collName))
                    {
                        database.CreateCollection(collName);
                    }
                    var collection = database.GetCollection(collName);
                    if (collection != null)
                    {
                        string pathFile = ConfigurationManager.AppSettings["PathFile"];
                        if (args.Length > 1 && args[1] == "-clear")
                            ClearFolder(pathFile);

                        DateTime dtBegin = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 0, 0, 0);

                        IMongoQuery query = Query.GTE("CreationDate", dtBegin);
                        var docs = collection.Find(query);

                        var wrkDirs = new HashSet<string>();

                        foreach (var doc in docs)
                        {
                            ExtractCorpora(pathFile, doc, wrkDirs);
                        }

                        
                        foreach (var wrkDir in wrkDirs)
                        {
                            BuildWordCloud(wrkDir);
                            if (enableWordCloud)
                            {
                                TweetWordCloud(wrkDir);
                            }                     
                        }

                    }
                }
                catch(MongoConnectionException mce)
                {
                    Console.Error.WriteLine(mce.Message);
                    Console.Error.WriteLine("MongoDB server not connected. Please retry...");
                    Thread.Sleep(5000);
                }
            }            
        }

        private static void TweetWordCloud(string pathFolderName)
        {
            Console.WriteLine("Publish word cloud in: " + pathFolderName);

            string status = "Tweet WordCloud";
            string topics = ConfigurationManager.AppSettings["Topics"];
            foreach (string topic in topics.Split(new char[]{' '}))
                status += string.Format(" #{0}", topic);
            string mentions = ConfigurationManager.AppSettings["Mentions"];
            foreach (string mention in mentions.Split(new char[] { ' ' }))
                status += string.Format(" @{0}", mention);

            var di = new System.IO.DirectoryInfo(pathFolderName);
            var files = di.GetFiles();
            var images = files.Where(f => f.Extension == ".png").OrderBy(f => f.CreationTime);
            if (images.Count() > 0)
            {
                var fi = images.LastOrDefault();

                Publish_tweet_with_text_and_image(status, fi.FullName);
            }
        }

        private static void Publish_tweet_with_text_and_image(string text, string image_path)
        {
            var serviceHelper = new TwitterServiceHelper();
            var service = serviceHelper.GetAuthenticatedService();
            using (var stream = new FileStream(image_path, FileMode.Open))
            {
                var tweet = service.SendTweetWithMedia(new SendTweetWithMediaOptions
                {
                    Status = text,
                    Images = new Dictionary<string, Stream> { { "word_cloud", stream } }
                });
            }
        }

        private static void ExtractCorpora(string pathFile, BsonDocument doc, HashSet<string> wrkDirs)
        {
            BsonElement elem;
            if (doc.TryGetElement("CreationDate", out elem))
            {
                var bdt = elem.Value.AsBsonDateTime;
                if (bdt.IsValidDateTime)
                {
                    string folderName = bdt.ToUniversalTime().ToShortDateString();
                    folderName = folderName.Replace("/", "-");
                    Console.WriteLine("Folder name: " + folderName);
                    string pathFolderName = System.IO.Path.Combine(pathFile, folderName);
                    if (!System.IO.Directory.Exists(pathFolderName))
                    {
                        System.IO.Directory.CreateDirectory(pathFolderName);
                    }
                    wrkDirs.Add(pathFolderName);

                    if (doc.TryGetElement("Id", out elem))
                    {
                        string fileName = elem.Value.ToString();
                        Console.WriteLine("File name: " + fileName);
                        string pathFileName = System.IO.Path.Combine(pathFolderName, fileName + ".txt");
                        if (doc.TryGetElement("Text", out elem))
                        {
                            string corpus = elem.Value.AsString;
                            Console.WriteLine("Corpus: " + corpus);
                            var sw = new System.IO.StreamWriter(pathFileName, false);
                            sw.Write(corpus);
                            sw.Flush();
                            sw.Close();
                        }
                    }
                }
            }
        }

        private static void ClearFolder(string pathFile)
        {
            var di = new System.IO.DirectoryInfo(pathFile);
            if (!di.Exists)
                di.Create();
            foreach (var fi in di.GetFiles())
                fi.Delete();
        }

        private static void BuildWordCloud(string pathFolderName)
        {
            Console.WriteLine("Build word cloud in: " + pathFolderName);

            string rExec = ConfigurationManager.AppSettings["Exec"];
            string rCmdLine = ConfigurationManager.AppSettings["CmdLine"];
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(rExec, rCmdLine);
                psi.CreateNoWindow = false;
                psi.WorkingDirectory = Path.GetFullPath(pathFolderName);
                psi.ErrorDialog = false;
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.ErrorDataReceived += new DataReceivedEventHandler(process_ErrorDataReceived);
                    p.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
                    p.Start();

                    if (false == p.WaitForExit(60000))
                        throw new ArgumentException("The program '{0}' did not finish in time, aborting.", rExec);

                    if (p.ExitCode != 0)
                        throw new ArgumentException("Executables exit code is not 0, this is treated as an exception");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }

        static void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Error.WriteLine(e.Data);
            }
        }
        static void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Error.WriteLine(e.Data);
            }
        }

        class TwitterServiceHelper
        {
            private readonly string _hero;
            private readonly string _consumerKey;
            private readonly string _consumerSecret;
            private readonly string _accessToken;
            private readonly string _accessTokenSecret;

            public TwitterServiceHelper()
            {
                _hero = ConfigurationManager.AppSettings["Hero"];
                _consumerKey = ConfigurationManager.AppSettings["ConsumerKey"];
                _consumerSecret = ConfigurationManager.AppSettings["ConsumerSecret"];
                _accessToken = ConfigurationManager.AppSettings["AccessToken"];
                _accessTokenSecret = ConfigurationManager.AppSettings["AccessTokenSecret"];
            }

            public TwitterService GetAuthenticatedService()
            {
                var service = new TwitterService(_consumerKey, _consumerSecret);
                service.TraceEnabled = true;
                service.AuthenticateWith(_accessToken, _accessTokenSecret);
                return service;
            }
        }
    }
}
