using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Net;


namespace Toy
{
    class Program
    {
        static FileMetaData ParseFromFile(string path)
        {
            var str = File.ReadAllText(path);
            return FileMetaData.Deserialize(str);
        }
        static void Main(string[] args)
        {
            List<FileMetaData> downloadList = new List<FileMetaData>();
            List<FileMetaData> uploadList = new List<FileMetaData>();
            for(var i = 0; i < args.Length;)
            {
                if (args[i][0] == '-')
                {
                    if (args[i] == "-D" || args[i] == "--download")
                    {
                        i++;
                        downloadList.Add(ParseFromFile(args[i]));
                        i++;
                    }
                    else if (args[i] == "-U" || args[i] == "--upload")
                    {
                        i++;
                        uploadList.Add(ParseFromFile(args[i]));
                        i++;
                    }
                    else if (args[i] == "-G" || args[i] == "--generate")
                    {
                        i++;
                        var name = args[i];
                        var ip = IPAddress.Parse(args[++i]);
                        var name_b = Encoding.UTF8.GetBytes(name);
                        var length = 0;
                        using (var fs = File.Open(name, FileMode.Open))
                        {
                            length = (int)fs.Length;
                        }
                        FileMetaData data = new FileMetaData(name,
                                SHA256.Create().ComputeHash(name_b), ip, length);
                        File.WriteAllText(name + ".meta", FileMetaData.Serialize(data));
                        i++;
                    }
                    else
                    {
                        Console.WriteLine("invaild command");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("invaild command");
                    return;
                }
            }

            // 初始化工作
            LocalIP.Init();
            Logger.Init();
            if (downloadList.Count != 0 || uploadList.Count != 0)
            {
                var fmanager = new FileManager();
                var client = new Client(downloadList, uploadList, fmanager);
                client.Start();
            }
        }
    }
}
