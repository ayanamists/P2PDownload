using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Linq;

namespace Toy
{
    class LocalIP
    {
        static List<IPAddress> iPs;
        static public void Init()
        {
            var strHostName = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
            IPAddress[] addr = ipEntry.AddressList;
            iPs = new List<IPAddress>(addr);
        }
        static public bool IfLocalIp(IPAddress iP)
        {
            return iPs.Contains(iP);
        }
        static public IPAddress GetClosestIPAddress(IPAddress ip)
        {
            var target = ConvertIP(ip);
            var res = (target, IPAddress.Parse("0.0.0.0"));
            foreach(var i in iPs)
            {
                var now = (ConvertIP(i) - target, i);
                if(now.Item1 < res.target)
                {
                    res = now;
                }
            }
            return res.Item2;
        }
        static public uint ConvertIP(IPAddress ip)
        {
            return BitConverter.ToUInt32(ip.GetAddressBytes().Reverse().ToArray());
        }
    }
}
