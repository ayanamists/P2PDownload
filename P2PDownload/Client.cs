using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Net.Sockets;
using BinarySerialization;
using System.Text.Json;

namespace Toy
{
    [Serializable]
    class FileMetaData
    {
        public readonly string Name;
        public readonly byte[] Hash;
        public readonly IPAddress Server;
        public readonly int Size;
        public readonly int BlockCount;
        public FileMetaData(string name, byte[] hash, IPAddress server, int size)
        {
            Name = name;
            Hash = hash;
            Server = server;
            Size = size;
            const int BlockSize = 4096;
            BlockCount = (size % BlockSize == 0) ? size / 4096 : size / 4096 + 1;
        }
        public FileMetaData(string buffer)
        {
            var i = (FileMetaData)JsonSerializer.Deserialize(buffer, this.GetType());
        }
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
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
    }
    class FileStat
    {
        public IPAddress Server { get; set; }
        private List<byte[]> hashOfBlock;
        public List<LocateInfo> locateInfos { get; set; }
        private List<bool> IfPrsent;
        public List<int> FailBlock;
        public int Size { get; }
        public string Name { get; }
        public int BlockCount { get; set; }
        public FileStat(List<byte[]> hash_values, List<LocateInfo> locateInfo_values)
        {
            hashOfBlock = hash_values;
            locateInfos = locateInfo_values;
            IfPrsent = new List<bool>(hashOfBlock.Count);
            for(int i = 0; i < IfPrsent.Count; ++i) { IfPrsent[i] = false; }
            FailBlock = new List<int>();
        }
        public FileStat(int size, string name, IPAddress server)
        {
            // IfDownload = new List<bool>(hashOfBlock.Count);
            for (int i = 0; i < IfPrsent.Count; ++i) { IfPrsent[i] = false; }
            locateInfos = null;
            Size = size;
            BlockCount = (Size % 4096 == 0) ? Size / 4096 : (Size / 4096) + 1;
            IfPrsent = new List<bool>(BlockCount);
            Name = name;
            Server = server;
        }
        public bool IfKnowLocate()
        {
            return locateInfos == null;
        }
        public void AddLocateInfo(IPAddress ip, ICollection<int> index_value)
        {
            bool if_find = false;
            foreach(var i in locateInfos)
            {
               if(i.IP == ip)
                {
                    if_find = true;
                    i.AddNewInfo(index_value);
                }
            }
            if (!if_find)
            {
                locateInfos.Add(new LocateInfo(ip, index_value));
            }
        }
        public bool GetLocalPresentStat(int index) { return IfPrsent[index]; }
        public void SetLocalPresentStat(int index, bool value)
        {
            IfPrsent[index] = value;
        }
        public IPAddress FindIndex(int index)
        {
            foreach(var i in locateInfos)
            {
                if(i.Indexs.Contains(index))
                {
                    return i.IP;
                }
            }
            return Server;
        }
        
        public void AddFailIndex(int index)
        {
            FailBlock.Add(index);
        }
        public void SetRemotePresentStat(IPAddress ip, int index, bool value)
        {
            foreach(var i in locateInfos)
            {
                if(i.IP == ip)
                {
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
        }
    }
    class FileManager : IDisposable
    {
        const int BlockSize = 4096;
        private Dictionary<byte[], int> hashToFIndex;
        private List<byte[]> indexToHash;
        private List<FileStat> fileStats;
        private List<FileStream> fileStreams;

        public FileManager()
        {
            hashToFIndex = new Dictionary<byte[], int>();
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
        public void AddNewFile(FileMetaData metaData)
        {
            var fileStat = new FileStat(metaData.Size, metaData.Name, metaData.Server);
            indexToHash.Add(metaData.Hash);
            var pos = indexToHash.Count - 1;
            hashToFIndex.Add(metaData.Hash, pos);
            fileStats.Add(fileStat);
        }
        public void AddNewDownloadFile(FileMetaData metaData)
        {
            AddNewFile(metaData);
            InitNewFile();
        }
        public async void WriteToFileAsync(byte[] hash, int index, byte[] what)
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
            var ret = new byte[size];
            fs.Read(ret, 0, size);
            return ret;
        }
        private void InitNewFile()
        {
            var stat = fileStats[fileStats.Count - 1];
            FileStream file = new FileStream(stat.Name, FileMode.Create);
            fileStreams.Add(file);
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
                AddNewFile(i);
                openExistFile(i);
            }
        }
        public void AddOrRefreshFileInfo(byte[] hash, IPAddress ip, ICollection<int> indexs)
        {
            fileStats[GetFindex(hash)].AddLocateInfo(ip, indexs);
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
            fileStats[GetFindex(hash)].SetRemotePresentStat(ip, index, value);
        }
        public bool IfDownloadIndex(byte[] hash, int index)
        {
            return fileStats[GetFindex(hash)].GetLocalPresentStat(index);
        }
        public void OnDownloadFail(byte[] hash, int index)
        {
            fileStats[GetFindex(hash)].AddFailIndex(index);
        }
        public void OnDownloadSuccess(byte[] hash, int index, byte[] data)
        {
            WriteToFileAsync(hash, index, data);
            fileStats[GetFindex(hash)].SetLocalPresentStat(index, true);
        }
        public List<LocateInfo> ProvideLocateInfo(byte[] hash)
        {
            return fileStats[GetFindex(hash)].locateInfos;
        }
        public byte[] ProvideFileData(byte[] hash, int index)
        {
            var fIndex = GetFindex(hash);
            return ReadToBuffer(fIndex, index);
        }

        public void Dispose()
        {
            foreach(var i in fileStreams)
            {
                i.Dispose();
            }
        }
    }
    class DownloadTransManager : IDisposable
    {
        const int uniPort = 4000;
        IPAddress target;
        TcpClient tcpCilent;
        BinarySerializer serializer;
        MemoryStream ms;
        public DownloadTransManager(IPAddress iPAddress)
        {
            serializer = new BinarySerializer();
            target = iPAddress;
            tcpCilent = new TcpClient(new IPEndPoint(target, uniPort));
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
            send(getBinaryMessage(message));
        }
        void ReadToBuffer()
        {
            var s = tcpCilent.GetStream();
            var buffer = new byte[1000];
            while (true)
            {
                tcpCilent.ReceiveTimeout = 80;
                try
                {
                    do
                    {
                        var size = s.Read(buffer, 0, 1000);
                        ms.Write(buffer, 0, size);
                    } while (s.DataAvailable);
                }
                catch (IOException)
                {
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
        public List<P2PMessage> GetReply()
        {
            ReadToBuffer();
            return P2PMessage.GenFromMS(ms);
        }
        public void Dispose()
        {
            tcpCilent.Close();
            ms.Dispose();
        }
    }
    class  DownloadManager
    {
        private FileManager fmanager;
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
                transMan.SendCloseMessage();
                result = transMan.GetReply();
            }
            foreach (var i in result)
            {
                if (i.type == P2PMessageType.FILE_INFO)
                {
                    var info = (FileInfoMessage)i;
                    foreach (var j in info.locateInfos)
                    {
                        fmanager.AddOrRefreshFileInfo(
                            j.hash, new IPAddress(j.locate), j.indexs); 
                    }
                    break;
                }
            }
        }
        void DownloadData(byte[] hash, int index, IPAddress iP)
        {
            var failStat = $"target: {iP} don't have this block";
            List<P2PMessage> result;
            using (var transMan = new DownloadTransManager(iP))
            {
                transMan.GetData(hash, new int[1] { index });
                result = transMan.GetReply();
                if (result.Count == 0) {
                    transMan.SendCloseMessage();
                    throw new FailToDownloadException(failStat); 
                }
                foreach (var i in result)
                {
                    if (i.type == P2PMessageType.DATA)
                    {
                        var data = (DataMessage)i;
                        if (data.p2PDatas.block.index == index)
                        {
                            fmanager.OnDownloadSuccess(hash, index, data.p2PDatas.data);
                            transMan.SendOkMessage(hash, index);
                            return;
                        }
                    }
                    else if (i.type == P2PMessageType.ERROR)
                    {
                        fmanager.SetRemoteBlockPresentStat(hash, iP, index, false);
                        if (((ErrorMessage)i).Block.index == index)
                        {
                            throw new FailToDownloadException(failStat);
                        }
                    }
                    throw new FailToDownloadException(failStat);
                }
            }
        }
        public void DownloadLoop(FileMetaData metaData)
        {
            var hash = metaData.Hash;
            var blockCount = metaData.BlockCount;
            fmanager.AddNewDownloadFile(metaData);
            Queue<int> indexs = new Queue<int>(blockCount);
            for(var i = 0; i < blockCount; ++i) { indexs.Enqueue(i); }
            IPAddress lastDownLoc = metaData.Server;
            int loopCount = 0;
            while(indexs.Count != 0)
            {
                var index = indexs.Dequeue();
                if (fmanager.IfDownloadIndex(hash, index)) { }
                else {
                    if (loopCount % 5 == 0)
                    {
                        RefreshFileInfo(lastDownLoc, hash);
                    }
                    else
                    {
                        try
                        {
                            DownloadData(hash, index, fmanager.GetConsultForDownload(hash, index));
                        }
                        catch(FailToDownloadException)
                        {
                            try
                            {
                                DownloadData(hash, index, metaData.Server);
                            }
                            catch (FailToDownloadException)
                            {
                                fmanager.OnDownloadFail(hash, index);
                            }
                        }
                    }
                }
                loopCount++;
            }
        }
    }
    class UploadManager
    {
        const int listenPort = 4000;
        FileManager fmanager;
        TcpListener listener;
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
                handleLoop(client);
                client.Dispose();
            }
        }
        void handleLoop(TcpClient client)
        {
            client.ReceiveTimeout = 100;
            var s = client.GetStream();
            bool ifClose = false;
            while (!ifClose)
            {
                try
                {
                    var buffer = new byte[1000];
                    var ms = new MemoryStream();
                    do
                    {
                        var size = s.Read(buffer, 0, 1000);
                        ms.Write(buffer, 0, size);
                    } while (s.DataAvailable);
                    var what = P2PMessage.GenFromMS(ms);
                    foreach(var i in what)
                    {
                        if(i.type == P2PMessageType.GET_FILE_INFO)
                        {
                            var hash = ((GetFileInfoMessage)i).hash;
                            SendFileInfoMessage(fmanager.ProvideLocateInfo(hash), hash, client);
                        }
                        else if(i.type == P2PMessageType.GET_DATA)
                        {
                            var message = (GetDataMessage)i;
                            var hash = message.hash;
                            var indexs = message.what;
                            byte[] send_buffer;
                            try
                            {
                                send_buffer = fmanager.ProvideFileData(hash, indexs[0]);
                                send(new DataMessage(send_buffer, hash, indexs[0]), client);
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
                    break;
                }
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
                var array = new int[i.Indexs.Count];
                i.Indexs.CopyTo(array);
                FileLocateInfo info = new FileLocateInfo(i.IP.GetAddressBytes(), 
                    hash, i.Indexs.Count, array);
                locateInfos[count++] = info;
            }
            var FileInfoMessage = new FileInfoMessage(locateInfos);
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
            var dm = new DownloadManager(_fileManager);
            foreach(var i in _fileToDownload)
            {
                dm.DownloadLoop(i);
            }
        }
        public void StartUpload()
        {
            var um = new UploadManager(_fileManager, _fileToUpload);
            um.StartListenLoop();
        }
    }
}
