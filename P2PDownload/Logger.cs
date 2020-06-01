using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using log4net;
using log4net.Config;

namespace Toy
{
    class Logger
    {
        static string repo;
        public static void Init()
        {
            repo = "Toy";
            var repo_value = LogManager.CreateRepository(repo);
            XmlConfigurator.Configure(repo_value, new FileInfo("log.config"));
        }
        public static ILog GetLogger(Type type)
        {
            return LogManager.GetLogger(repo, type);
        }
    }
}
