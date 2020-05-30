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
            return new FileMetaData(str);
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
                        using (var fs = File.Open(name, FileMode.Open))
                        {
                            FileMetaData data = new FileMetaData(name,
                                SHA256.Create().ComputeHash(name_b), ip, (int)fs.Length);
                            File.WriteAllText(name + ".meta", data.Serialize());
                        }
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
            if(downloadList.Count != 0 || uploadList.Count != 0)
            {
                var fmanager = new FileManager();
                var client = new Client(downloadList, uploadList, fmanager);
                client.Start();
            }
        }
    }
}
