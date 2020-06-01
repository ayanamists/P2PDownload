using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using System.Net.Sockets;
using BinarySerialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using log4net;
using System.Collections;
using System.Diagnostics;
using System.Linq;

namespace Toy
{
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        //    Private backing field for the Default property below.
        private static ByteArrayComparer _default;

        /// <summary>
        ///    Default instance of <see cref = "ByteArrayComparer"/>
        /// </summary>
        public static ByteArrayComparer Default
        {
            get
            {
                if (_default == null)
                {
                    _default = new ByteArrayComparer();
                }

                return _default;
            }
        }

        /// <summary>
        ///    Tests for equality between two byte arrays based on their value
        ///    sequences.
        ///	<param name = "obj1">A byte array to test for equality against obj2.</param>
        /// <param name = "obj2">A byte array to test for equality againts obj1.</param>
        /// </summary>
        public bool Equals(byte[] obj1, byte[] obj2)
        {
            //    We can make use of the StructuralEqualityComparar class to see if these
            //    two arrays are equaly based on their value sequences.
            return StructuralComparisons.StructuralEqualityComparer.Equals(obj1, obj2);
        }

        /// <summary>
        ///    Gets a hash code to identify the given object.
        /// </summary>
        /// <param name = "obj">The byte array to generate a hash code for.</param>
        public int GetHashCode(byte[] obj)
        {
            //    Just like in the Equals method, we can use the StructuralEqualityComparer
            //    class to generate a hashcode for the object.
            return StructuralComparisons.StructuralEqualityComparer.GetHashCode(obj);
        }
    }
    [Serializable]
    class FileMetaData
    {
        public string Name { get; set; }
        public byte[] Hash { get; set; }
        [JsonIgnore]
        public IPAddress Server { get; set; }
        public string ServerString { get; set; }
        public int Size { get; set; }
        public int BlockCount { get; set; }
        public List<byte[]> BlockHashs { get; set; }
        public FileMetaData(string name, byte[] hash, IPAddress server, int size)
        {
            Name = name;
            Hash = hash;
            Server = server;
            ServerString = server.ToString();
            Size = size;
            const int BlockSize = 4096;
            BlockCount = (size % BlockSize == 0) ? size / 4096 : size / 4096 + 1;
            BlockHashs = new List<byte[]>(BlockCount);
            using(var fs = File.OpenRead(name))
            {
                for(int i = 0; i < BlockCount; ++i)
                {
                    var buffer = new byte[BlockSize];
                    fs.Read(buffer, 0, BlockSize);
                    BlockHashs.Add(SHA256.Create().ComputeHash(buffer));
                }
            }
        }
        public FileMetaData()
        {
        }
        static public FileMetaData Deserialize(string buffer)
        {
            var i = (FileMetaData)JsonSerializer.Deserialize(buffer, typeof(FileMetaData));
            i.Server = IPAddress.Parse(i.ServerString);
            return i;
        }
        static public string Serialize(FileMetaData meta)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            return JsonSerializer.Serialize(meta, options);
        }
    }
    class LocateInfo
    {
        public IPAddress IP { get; set; }
        public HashSet<int> Indexs { get; set; }
        public LocateInfo(IPAddress ip_value, ICollection<int> indexs_value) { 
            IP = ip_value;
            Indexs = new HashSet<int>(indexs_value);
        }
        public void AddNewInfo(ICollection<int> new_info)
        {
            Indexs.UnionWith(new_info);
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(IP);
            sb.Append("\nBlocks: ");
            foreach(var i in Indexs)
            {
                sb.Append($"{i} ");
            }
            return sb.ToString();
        }
    }
    class FileStat
    {
        public IPAddress Server { get; set; }
        public List<byte[]> HashOfBlock { get; set; }
        public List<LocateInfo> locateInfos { get; set; }
        public List<bool> IfPrsent { get; }
        public List<int> FailBlock;
        public int Size { get; }
        public string Name { get; }
        public int BlockCount { get; set; }
        public ILog _logger = LogManager.GetLogger(typeof(FileStat));
        public int GetIP(IPAddress iP)
        {
            int pos = 0;
            foreach(var i in locateInfos)
            {
                if (i.IP.Equals(iP))
                {
                    return pos;
                }
                pos++;
            }
            return -1;
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(var i in locateInfos)
            {
                sb.Append(i.ToString());
                sb.Append("\n");
            }
            return sb.ToString();
        }
        
        public bool IfFileComplete()
        {
            var i = true;
            foreach (var j in IfPrsent)
            {
                if (!j) { i = false; break; }
            }
            return i;
        }
        public FileStat(FileMetaData metaData, bool present)
        {
            var size = metaData.Size;
            var name = metaData.Name;
            var server = metaData.Server;
            HashOfBlock = metaData.BlockHashs;
            locateInfos = new List<LocateInfo>();
            Size = size;
            BlockCount = (Size % 4096 == 0) ? Size / 4096 : (Size / 4096) + 1;
            IfPrsent = new List<bool>(BlockCount);
            for (int i = 0; i < BlockCount; ++i) { IfPrsent.Add(present); }
            Name = name;
            Server = server;
            FailBlock = new List<int>();
        }
        public bool IfKnowLocate()
        {
            return locateInfos.Count == 0;
        }
        public void AddOrRefreshLocateInfo(IPAddress ip, ICollection<int> index_value)
        {
            var a = GetIP(ip);
            if (a == -1)
            {
                locateInfos.Add(new LocateInfo(ip, index_value));
            }
            else
            {
                locateInfos[a].AddNewInfo(index_value);
            }
        }
        public bool GetLocalPresentStat(int index) { return IfPrsent[index]; }
        public void SetLocalPresentStat(int index, bool value)
        {
            IfPrsent[index] = value;
        }
        public IPAddress FindIndex(int index)
        {
            if (locateInfos.Count > 0)
            {
                foreach (var i in locateInfos)
                {
                    if (i.Indexs.Contains(index))
                    {
                        return i.IP;
                    }
                }
            }
            else { } 
            return Server;
        }
        
        public void AddFailIndex(int index)
        {
            FailBlock.Add(index);
        }
        public void SetRemotePresentStat(IPAddress ip, int index, bool value)
        {
            var find = false;
            foreach(var i in locateInfos)
            {
                if(i.IP.Equals(ip))
                {
                    find = true;
                    if (value)
                    {
                        i.Indexs.Add(index);
                    }
                    else
                    {
                        i.Indexs.Remove(index);
                    }
                }
            }
            if (!find && value)
            {
                var fileInfo = new LocateInfo(ip, new int[] { index });
                locateInfos.Add(fileInfo);
            }
        }
        public LocateInfo GetLocallLocateInfo(IPAddress iP)
        {
            var indexs = new List<int>();
            for(int i = 0; i < IfPrsent.Count; ++i)
            {
                if (IfPrsent[i])
                {
                    indexs.Add(i);
                }
            }
            return new LocateInfo(iP, indexs);
        }
    }
    class FileManager : IDisposable
    {
        const int BlockSize = 4096;
        private Dictionary<byte[], int> hashToFIndex;
        private List<byte[]> indexToHash;
        private List<FileStat> fileStats;
        private List<FileStream> fileStreams;
        static ILog _logger = Logger.GetLogger(typeof(FileManager));
        public FileManager()
        {
            hashToFIndex = new Dictionary<byte[], int>(ByteArrayComparer.Default);
            indexToHash = new List<byte[]>();
            fileStats = new List<FileStat>();
            fileStreams = new List<FileStream>();
        }
        public int GetFindex(byte[] what) {
            int ret = 0;
            hashToFIndex.TryGetValue(what, out ret);
            return ret;
        }
        public byte[] GetHash(int index)
        {
            return indexToHash[index];
        }
        public void AddNewFile(FileMetaData metaData, bool Present)
        {
            var fileStat = new FileStat(metaData, Present);
            indexToHash.Add(metaData.Hash);
            var pos = indexToHash.Count - 1;
            hashToFIndex.Add(metaData.Hash, pos);
            fileStats.Add(fileStat);
        }
        public void AddNewDownloadFile(FileMetaData metaData)
        {
            AddNewFile(metaData, false);
            InitNewFile();
        }
        public async void WriteToFile(byte[] hash, int index, byte[] what)
        {
            var Findex = GetFindex(hash);
            var fs = fileStreams[Findex];
            var stat = fileStats[Findex];
            fs.Position = index * BlockSize;
            var size =
                ((index * BlockSize + BlockSize) > stat.Size) ?
                (stat.Size % BlockSize) :
                BlockSize;
            await fs.WriteAsync(what, 0, size);
            _logger.Info($"write to file, index : {index}");
        }
        byte[] ReadToBuffer(int Findex, int index)
        {
            var fs = fileStreams[Findex];
            fs.Position = index * BlockSize;
            var stat = fileStats[Findex];
            if (!stat.GetLocalPresentStat(index))
            {
                throw new BlockNotPresentException($"Block {index} Of {stat.Name} not Present!");
            }
            var size =
                ((index * BlockSize + BlockSize) > stat.Size) ?
                (stat.Size % BlockSize) :
                BlockSize;
            var ret = new byte[BlockSize];
            fs.Read(ret, 0, size);
            return ret;
        }
        private void InitNewFile()
        {
            var stat = fileStats[fileStats.Count - 1];
            if (File.Exists(stat.Name))
            {
                throw new HasBeenDownloadException($"filename : {stat.Name} has been download!");
            }
            else
            {
                FileStream file = new FileStream(stat.Name, FileMode.Create);
                // var array = new byte[stat.Size];
                // file.Write(array, 0, stat.Size);
                fileStreams.Add(file);
            }
        }
        void openExistFile(FileMetaData metaData)
        {
            var name = metaData.Name;
            FileStream file = new FileStream(name, FileMode.Open);
            fileStreams.Add(file);
        }
        public void AddNewUploadFile(List<FileMetaData> metaDatas)
        {
            foreach(var i in metaDatas)
            {
                AddNewFile(i, true);
                openExistFile(i);
            }
        }
        public void AddOrRefreshFileInfo(byte[] hash, IPAddress ip, ICollection<int> indexs)
        {
            lock (fileStats[GetFindex(hash)])
            {
                // 如果Ip是本机ip，要相信自己的数据，而不是外面传回的数据
                if (!LocalIP.IfLocalIp(ip))
                {
                    fileStats[GetFindex(hash)].AddOrRefreshLocateInfo(ip, indexs);
                }
            }
        }
        public IPAddress GetConsultForDownload(byte[] hash, int index)
        {
            return fileStats[GetFindex(hash)].FindIndex(index);
        }
        private void CloseFile(int ptr)
        {
            fileStreams[ptr].Close();
        }
        
        public void SetRemoteBlockPresentStat(byte[] hash, IPAddress ip, int index, bool value)
        {
            lock (fileStats[GetFindex(hash)])
            {
                // 如果Ip是本机ip，要相信自己的数据，而不是外面传回的数据
                if (!LocalIP.IfLocalIp(ip))
                {
                    fileStats[GetFindex(hash)].SetRemotePresentStat(ip, index, value);
                }
                _logger.Debug($"fileStat of {hash}\n {fileStats[GetFindex(hash)]}");
            }
        }
        public bool IfDownloadIndex(byte[] hash, int index)
        {
            return fileStats[GetFindex(hash)].GetLocalPresentStat(index);
        }
        public void OnBlockDownloadFail(byte[] hash, int index)
        {
            lock (fileStats[GetFindex(hash)])
            {
                fileStats[GetFindex(hash)].AddFailIndex(index);
            }
        }
        public void OnBlockDownloadSuccess(byte[] hash, int index, byte[] data)
        {
            lock (fileStats[GetFindex(hash)])
            {
                if (CompareHash(hash, data, index))
                {
                    WriteToFile(hash, index, data);
                    fileStats[GetFindex(hash)].SetLocalPresentStat(index, true);
                }
                else
                {
                    _logger.Debug($"hash of Block {index} not equal to metaData");
                    throw new FailToDownloadException($"block {index} not accepted");
                }
            }
        }

        /*
         *  这个函数返回某个文件所有的位置信息；由于fileStats数组中没有自己的信息，
         *  所以需要把自己的信息（如果有的话）附加上。
         *  由于在我们的设定中，server的信息是不返回的，
         *  所以还要进行一下判断，如果hash的Server是本机，则不返回本机信息
         */
        public List<LocateInfo> ProvideLocateInfo(byte[] hash, IPAddress localIP)
        {
            if (!hashToFIndex.ContainsKey(hash)) { return new List<LocateInfo>(); }
            else
            {
                var stat = fileStats[GetFindex(hash)];
                var info = stat.locateInfos;
                var local_info = stat.GetLocallLocateInfo(localIP);

                // 本机有这个文件的信息 且 这个文件的Server不是本机
                if (!LocalIP.IfLocalIp(stat.Server) &&
                    local_info.Indexs.Count != 0) 
                {
                    info.Add(local_info);
                }
                return info;
            }
        }
        public byte[] ProvideFileData(byte[] hash, int index)
        {
            var fIndex = GetFindex(hash);
            if (!fileStreams[fIndex].CanRead) { 
                fileStreams[fIndex] = File.OpenRead(fileStats[fIndex].Name);
            }
            return ReadToBuffer(fIndex, index);
        }

        public void Dispose()
        {
            foreach(var i in fileStreams)
            {
                if (i.CanRead)
                {
                    i.Close();
                    i.Dispose();
                }
            }
        }
        public void OnDownloadException(byte[] hash)
        {
            var findex = GetFindex(hash);
            var fs = fileStreams[findex];
            fs.Close();
            if (!fileStats[findex].IfFileComplete())
            {
                Directory.Move(fileStats[findex].Name, $"{fileStats[findex].Name}.bak");
            }
            fs.Dispose();
        }
        
        // 保证this_block_data的空部分全为零
        public bool CompareHash(byte[]hash_of_file, byte[] this_block_data, int index)
        {
            var fIndex = GetFindex(hash_of_file);
            var thisTimeHash = SHA256.Create().ComputeHash(this_block_data, 0, BlockSize);
            return (new ByteArrayComparer()).Equals(thisTimeHash, 
                fileStats[fIndex].HashOfBlock[index]);
        }
        public void OnDownloadComplete(byte[] hash)
        {
            var findex = GetFindex(hash);
            var stat = fileStats[findex];
            if (!stat.IfFileComplete())
            {
                StringBuilder sb = new StringBuilder();
                for(int i = 0; i < stat.IfPrsent.Count; ++i)
                {
                    if (!stat.IfPrsent[i])
                    {
                        sb.Append($"{i} ");
                    }
                }
                _logger.Error($"someblock of {stat.Name} can't be download, that is {sb}");
                OnDownloadException(hash);
            }
            var fs = fileStreams[findex];
            fs.Close();
        }
        public List<int> ProvideFailBlock(byte[] hash)
        {
            var findex = GetFindex(hash);
            return fileStats[findex].FailBlock;
        }
    }
    class DownloadTransManager : IDisposable
    {
        const int uniPort = 4000;
        IPAddress target;
        TcpClient tcpCilent;
        BinarySerializer serializer;
        MemoryStream ms;
        static readonly ILog _logger = 
            Logger.GetLogger(typeof(DownloadTransManager));
        public DownloadTransManager(IPAddress iPAddress)
        {
            serializer = new BinarySerializer();
            target = iPAddress;
            tcpCilent = new TcpClient();
            var result = tcpCilent.BeginConnect(target, uniPort, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
            if (!success)
            {
                throw new FailToConnectException($"Fail To Connect peer {iPAddress}");
            }
            ms = new MemoryStream();
        }
        byte[] getBinaryMessage(P2PMessage p2PMessage)
        {
            MemoryStream ms = new MemoryStream();
            serializer.Serialize(ms, p2PMessage);
            byte[] send_buffer = ms.ToArray();
            return send_buffer;
        }
        void send(byte[] send_buffer)
        {
            var s = tcpCilent.GetStream();
            s.Write(send_buffer, 0, send_buffer.Length);
        }
        void send(P2PMessage message)
        {
            _logger.Info($"send {message.type}, length {message.length}");
            send(getBinaryMessage(message));
        }
        void ReadToBuffer(int maxMessage)
        {
            var s = tcpCilent.GetStream();

            // 调节缓冲区大小，修改此变量
            var buffer_size = 5000;
            var buffer = new byte[buffer_size];
            var length4Read = -1;
            var count = 0;
            var size = 0;
            while (count < maxMessage)
            {
                tcpCilent.ReceiveTimeout = 80;
                try
                {
                    do
                    {
                        if(length4Read == -1)
                        {
                            size = s.Read(buffer, 0, 8);
                            length4Read = P2PMessage.TryParseHeader(buffer);
                        }
                        else if (length4Read == 0) { count++; length4Read = -1; break; }
                        else
                        {
                            var read_size = (length4Read > buffer_size) ? buffer_size : length4Read;
                            size = s.Read(buffer, 0, read_size);
                            length4Read -= size;
                        }
                        _logger.Info($"read {size} bytes from socket, length for read is {length4Read}");
                        ms.Write(buffer, 0, size);
                    } while (s.DataAvailable);
                }
                catch (IOException)
                {
                    _logger.Info($"Recive timeout");
                    break;
                }
                
            }
        }
        public void GetData(byte[] hash, int[] indexs)
        {
            GetDataMessage getData = new GetDataMessage(hash, indexs);
            send(getData);
        }
        public void GetFileInfo(byte[] hash)
        {
            GetFileInfoMessage message = new GetFileInfoMessage(hash);
            send(message);
        }
        public void GetBlockInfo(byte[] hash, int index)
        {
            GetBlockInfoMessage message = new GetBlockInfoMessage(hash, index);
            send(message);
        }
       
        public void SendOkMessage(byte[] hash, int index)
        {
            var Ok = new OKMessage(hash, index);
            send(Ok); 
        }
        public void SendCloseMessage()
        {
            var close = new CloseMessage();
            send(close);
        }
        public List<P2PMessage> GetReply(int maxReplyMessage)
        {
            ReadToBuffer(maxReplyMessage);
            return P2PMessage.GenFromMS(ms);
        }
        public void Dispose()
        {
            if (tcpCilent.Connected)
            {
                tcpCilent.Close();
            }
            ms.Dispose();
        }
    }
    class  DownloadManager
    {
        private FileManager fmanager;
        static readonly ILog _logger = Logger.GetLogger(typeof(DownloadManager));
        public DownloadManager(FileManager manager)
        {
            fmanager = manager;
        }
        void RefreshFileInfo(IPAddress lastDownLoc, byte[] hash)
        {
            List<P2PMessage> result;
            using (var transMan = new DownloadTransManager(lastDownLoc))
            {
                transMan.GetFileInfo(hash);
                result = transMan.GetReply(1);
                transMan.SendCloseMessage();
            }
            foreach (var i in result)
            {
                if (i.type == P2PMessageType.FILE_INFO)
                {
                    if (i.length != 0)
                    {
                        var info = (FileInfoMessage)i;
                        foreach (var j in info.locateInfos)
                        {
                            fmanager.AddOrRefreshFileInfo(
                                j.hash, new IPAddress(j.locate), j.indexs);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary> 
        /// 返回未能成功下载的块标记
        /// </summary>
        List<int> DownloadData(byte[] hash, int[] indexs, IPAddress iP)
        {
            var failStat = $"target: {iP} don't have this block";
            List<P2PMessage> result;
            StringBuilder @string = new StringBuilder();
            foreach(var i in indexs)
            {
                @string.Append($"{i} ");
            }
            _logger.Info($"Now Download {@string} from {iP}");
            using (var transMan = new DownloadTransManager(iP))
            {
                transMan.GetData(hash, indexs);
                result = transMan.GetReply(indexs.Length);
                if (result.Count == 0) {
                    transMan.SendCloseMessage();
                    // 如果进入了这里，说明请求的IP里，请求的块一个也没有
                    return new List<int>(indexs);
                }
                HashSet<int> set = new System.Collections.Generic.HashSet<int>(indexs);
                foreach (var i in result)
                {
                    _logger.Info($"Get {i.type} from {iP}");
                    if (i.type == P2PMessageType.DATA)
                    {
                        var data = (DataMessage)i;
                        var index = data.p2PDatas.block.index;
                        if (set.Contains(index))
                        {
                            transMan.SendOkMessage(hash, data.p2PDatas.block.index);
                            try
                            {
                                fmanager.OnBlockDownloadSuccess(hash, index, data.p2PDatas.data);
                                set.Remove(index);
                            }
                            catch(FailToDownloadException)
                            {
                            }
                        }
                    }
                    else if (i.type == P2PMessageType.ERROR)
                    {
                        var index = ((ErrorMessage)i).Block.index;
                        fmanager.SetRemoteBlockPresentStat(hash, iP, index, false);
                    }
                }
                transMan.SendCloseMessage();
                return set.ToList();
            }
        }
        static int gcd(int a, int b)
        {
            int c = 1;
            while(c != 0)
            {
                c = a % b;
                a = b;
                b = c;
            }
            return a;
        }
        /// <summary>
        /// 下载序列，按这里序列里的顺序下载
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns> 
        public static Queue<int> GenBlockQueue(int size)
        {
            var rand = new Random();
            var rand_number = rand.Next(0, size - 1);
            while(gcd(rand_number, size) != 1)
            {
                rand_number = rand.Next(0, size - 1);
            }
            var ret = new Queue<int>(size);
            for(var i = 0; i < size; ++i)
            {
                ret.Enqueue((rand_number * i) % size);
            }
            return ret;
        }
        public void DownloadLoop(FileMetaData metaData)
        {
            var hash = metaData.Hash;
            var blockCount = metaData.BlockCount;
            fmanager.AddNewDownloadFile(metaData);
            Queue<int> indexs = GenBlockQueue(metaData.BlockCount);
            IPAddress lastDownLoc = metaData.Server;
            int loopCount = 0;
            while(indexs.Count != 0)
            {
                int count = 0;

                // 调节下载速率的最重要参数，1为最小值（为0是不能下载的）。
                const int MaxCount = 1;
                List<int> download_list = new List<int>();
                var index = indexs.Dequeue();
                var ip = fmanager.GetConsultForDownload(hash, index);
                download_list.Add(index);
                count++;
                while (count < MaxCount && indexs.Count != 0)
                {
                    index = indexs.Dequeue();
                    if(fmanager.GetConsultForDownload(hash, index) == ip)
                    {
                        download_list.Add(index);
                        count++;
                    }
                    else
                    {
                        indexs.Enqueue(index);
                        break;
                    }
                }
                var d_list = download_list.Where((x) => (!fmanager.IfDownloadIndex(hash, x))).ToArray();
                if (loopCount % 4 == 0)
                {
                    try
                    {
                        RefreshFileInfo(lastDownLoc, hash);
                    }
                    catch(FailToConnectException e)
                    {
                        _logger.Warn(e);
                    }
                }
                else { }
                /*
                 * 默认Server必须拥有所有的原始数据，如果FileManager推荐的地址下载失败，
                 * 到Server处下载，如果再次失败，记录这个文件块为失败的文件块。
                 */
                var failedThisTime = DownloadData(hash, d_list, ip);
                if(failedThisTime.Count != 0)
                {
                    failedThisTime = DownloadData(hash, failedThisTime.ToArray(), ip);
                }
                foreach(var i in failedThisTime)
                {
                    fmanager.OnBlockDownloadFail(hash, i);
                }
                loopCount++;
            }
            var failed = fmanager.ProvideFailBlock(hash);
            // 再次尝试下载失败的数据块
            if(failed.Count != 0)
            {
                DownloadData(hash, failed.ToArray(), metaData.Server);
            }
            fmanager.OnDownloadComplete(metaData.Hash);
        }
    }
    class UploadManager
    {
        const int listenPort = 4000;
        FileManager fmanager;
        TcpListener listener;
        static readonly ILog _logger = Logger.GetLogger(typeof(UploadManager));
        public UploadManager(FileManager manager, List<FileMetaData> metaDatas)
        {
            listener = new TcpListener(IPAddress.Any, listenPort);
            fmanager = manager;
            fmanager.AddNewUploadFile(metaDatas);
        }
        public void StartListenLoop()
        {
            listener.Start();
            while (true)
            {
                var client = listener.AcceptTcpClient();
                _logger.Debug($"Accept {client.Client.RemoteEndPoint}");
                var newThread = new Thread(new ParameterizedThreadStart(handleConnection));
                newThread.Start(client);
            }
        }
        void handleConnection(object obj)
        {
            var client = (TcpClient)obj;
            handleLoop(client);
            _logger.Debug($"Close {client.Client.RemoteEndPoint}");
            client.Close();
            client.Dispose();
        }
        void handleLoop(TcpClient client)
        {
            System.Timers.Timer t = new System.Timers.Timer(200);
            var s = client.GetStream();
            bool ifClose = false;
            Stopwatch stopwatch = new Stopwatch();
            var continueTime = 0L;
            bool ifAlive = true;
            while (!ifClose && client.Connected)
            {
                if(continueTime > 500)
                {
                    if (ifAlive)
                    {
                        continueTime = 0;
                        ifAlive = false;
                    }
                    else
                    {
                        ifClose = true;
                        break;
                    }
                }
                stopwatch.Start();
                try
                {
                    _logger.Info("enter loop");
                    var waitConstant = 1;
                    var count = 0;
                    var ms = new MemoryStream();
                    var length4Read = -1;
                    while (count < waitConstant)
                    {
                        client.ReceiveTimeout = 100;

                        // 如果想要调节缓冲区大小，修改此变量
                        var buffer_size = 5000;
                        var buffer = new byte[buffer_size];
                        var size = 0;
                        do
                        {
                            if (length4Read == 0) { count++;
                                break; }
                            else if (length4Read == -1)
                            {
                                size = s.Read(buffer, 0, 8);
                                length4Read = P2PMessage.TryParseHeader(buffer);
                                if(BitConverter.ToInt32(buffer) == (int)P2PMessageType.GET_OK)
                                {
                                    waitConstant = 2;
                                }
                                else
                                {
                                    waitConstant = 1;
                                }
                            }
                            else
                            {
                                var read_size = (length4Read > buffer_size) ? buffer_size : length4Read;
                                size = s.Read(buffer, 0, read_size);
                                length4Read = length4Read - size;
                            }
                            ms.Write(buffer, 0, size);
                            _logger.Info($"now recv buffer is {BitConverter.ToString(ms.ToArray())}, length for read is {length4Read}, count is {count}");
                        } while (s.DataAvailable);
                    }
                    var what = P2PMessage.GenFromMS(ms);
                    foreach(var i in what)
                    {
                        ifAlive = true;
                        _logger.Info($"recive message : {i.type}");
                        if(i.type == P2PMessageType.GET_FILE_INFO)
                        {
                            var hash = ((GetFileInfoMessage)i).hash;
                            // 在这里，我使用绑定到TCP四元组的本机地址作为传回的本机地址。
                            var localAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                            SendFileInfoMessage(fmanager.ProvideLocateInfo(hash, localAddress), hash, client);
                        }
                        else if(i.type == P2PMessageType.GET_DATA)
                        {
                            var message = (GetDataMessage)i;
                            var hash = message.hash;
                            var indexs = message.what;
                            byte[] send_buffer;
                            try
                            {
                                foreach (var index in indexs)
                                {
                                    send_buffer = fmanager.ProvideFileData(hash, index);
                                    send(new DataMessage(send_buffer, hash, index), client);
                                }
                            }
                            catch(BlockNotPresentException)
                            {
                                send(new ErrorMessage(hash, indexs[0]), client);
                            }
                        }
                        else if(i.type == P2PMessageType.GET_OK)
                        {
                            var message = (OKMessage)i;
                            var remote_ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                            fmanager.SetRemoteBlockPresentStat(
                                message.block.hashOfFile,
                                remote_ip,
                                message.block.index,
                                true);
                        }
                        else if(i.type == P2PMessageType.CLOSE)
                        {
                            ifClose = true;
                        }
                    }    
                }
                catch (IOException)
                {
                    _logger.Debug($"timeout for receive : {client.Client.RemoteEndPoint}");
                    break;
                }
                stopwatch.Stop();
                continueTime += stopwatch.ElapsedMilliseconds;
            }
        }
        static byte[] getBinaryMessage(P2PMessage p2PMessage)
        {
            var serializer = new BinarySerializer();
            MemoryStream ms = new MemoryStream();
            serializer.Serialize(ms, p2PMessage);
            byte[] send_buffer = ms.ToArray();
            return send_buffer;
        }
        static void send(byte[] send_buffer, TcpClient client)
        {
            var s = client.GetStream();
            s.Write(send_buffer, 0, send_buffer.Length);
        }
        static void send(P2PMessage message, TcpClient client)
        {
            _logger.Info($"send {message.type}");
            send(getBinaryMessage(message), client);
        }
        public void SendP2PData(byte[] data, byte[] hash, int index, TcpClient client)
        {
            DataMessage p2PData = new DataMessage(data, hash, index);
            send(p2PData, client);
        }
        public void SendFileInfoMessage(List<LocateInfo> fileInfos, byte[] hash, TcpClient client)
        {
            FileLocateInfo[] locateInfos = new FileLocateInfo[fileInfos.Count];
            var count = 0;
            foreach(var i in fileInfos)
            {
                lock (i.Indexs)
                {
                    var array = i.Indexs.ToArray();
                    FileLocateInfo info = new FileLocateInfo(i.IP.GetAddressBytes(),
                        hash, array.Count(), array);
                    locateInfos[count++] = info;
                }
            }
            var FileInfoMessage = new FileInfoMessage(locateInfos);
            _logger.Info($"send FILE_INFO message, count is {count}");
            send(FileInfoMessage, client);
        }
        public void SendBlockInfoMessage(byte[] hash, int index, TcpClient client)
        {
            var blockInfo = new BlockInfoMessage(hash, index);
        }
    }
    class Client
    {
        List<FileMetaData> _fileToDownload;
        List<FileMetaData> _fileToUpload;
        FileManager _fileManager;
        static private readonly ILog Logger = LogManager.GetLogger("Toy", typeof(Client));
        public Client(List<FileMetaData> fileToDownload, List<FileMetaData> fileToUpload, FileManager manager)
        {
            _fileToDownload = fileToDownload;
            _fileToUpload = fileToUpload;
            _fileManager = manager;
        }
        public void Start()
        {
            try
            {
                System.Threading.Tasks.Parallel.Invoke(StartDownload, StartUpload);
            }
            catch(AggregateException e)
            {
                Console.WriteLine("An action has thrown an exception. THIS WAS UNEXPECTED.\n{0}", e.InnerException.ToString());
            }
            finally
            {
                _fileManager.Dispose();
            }
        }
        public void StartDownload()
        {
            Logger.Info("Download Start");
            var dm = new DownloadManager(_fileManager);
            foreach(var i in _fileToDownload)
            {
                try
                {
                    dm.DownloadLoop(i);
                    Logger.Info($"Download {i.Name} complete");
                }
                catch(Exception e)
                {
                    Logger.Error(e);
                    _fileManager.OnDownloadException(i.Hash);
                }
            }
        }
        public void StartUpload()
        {
            Logger.Info("Upload Start");
            var um = new UploadManager(_fileManager, _fileToUpload);
            um.StartListenLoop();
        }
    }
}
