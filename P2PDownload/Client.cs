using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

namespace Toy
{
    class Block
    {
        public int block_index { get; set; }
        public int file_index { get; set; }
        public Block(int block_index_value,int file_index_value) {
            block_index = block_index_value;
            file_index = file_index_value;
        }
    }
    class Quenes
    {
        public static Queue<Block> DownloadQueue = new Queue<Block>();
        public static Queue<Block> UploadQueue = new Queue<Block>();
        public static Queue<Block> SuccessQueue = new Queue<Block>();
        public static Queue<Block> ErrorQueue = new Queue<Block>();
    }
    class LocateInfo
    {
        public IPAddress IP { get; set; }
        public List<int> Indexs { get; set; }
        public LocateInfo(IPAddress ip_value, List<int> indexs_value)
        {
            IP = ip_value;
            Indexs = indexs_value;
        }
    }
    class FileStat
    {
        private List<byte[]> hashOfBlock;
        private List<LocateInfo> locateInfos;
        private List<bool> IfDownload;
        public int Size { get; }
        public string Name { get; }
        public FileStat(List<byte[]> hash_values, List<LocateInfo> locateInfo_values)
        {
            hashOfBlock = hash_values;
            locateInfos = locateInfo_values;
            IfDownload = new List<bool>(hashOfBlock.Count);
            for(int i = 0; i < IfDownload.Count; ++i) { IfDownload[i] = false; }
        }
        public FileStat(List<byte[]> hash_values, int size, string name)
        {
            IfDownload = new List<bool>(hashOfBlock.Count);
            for (int i = 0; i < IfDownload.Count; ++i) { IfDownload[i] = false; }
            locateInfos = null;
            Size = size;
            Name = name;
        }
        public bool IfKnowLocate()
        {
            return locateInfos == null;
        }
        public void AddLocateInfo(IPAddress ip, List<int> index_value)
        {
            locateInfos.Add(new LocateInfo(ip, index_value));
        }
        public void SetDownloadStat(int index, bool value)
        {
            IfDownload[index] = value;
        }
    }
    class FileManager
    {
        const int BlockSize = 4096;
        private Dictionary<byte[], int> hashToFIndex;
        private List<byte[]> indexToHash;
        private List<FileStat> fileStats;
        private List<FileStream> fileStreams;
        public int GetFindex(byte[] what) {
            int ret = 0;
            hashToFIndex.TryGetValue(what, out ret);
            return ret;
        }
        public byte[] GetHash(int index)
        {
            return indexToHash[index];
        }
        public void AddNewDownloadFile(byte[] hash, FileStat fileStat)
        {
            indexToHash.Add(hash);
            var pos = indexToHash.Count - 1;
            hashToFIndex.Add(hash, pos);
            fileStats.Add(fileStat);
            InitNewFile();
        }
        public async void WriteToFileAsync(int Findex, int index, byte[] what)
        {
            var fs = fileStreams[Findex];
            var stat = fileStats[Findex];
            fs.Position = index * BlockSize;
            var size = 
                ((index * BlockSize + BlockSize) > stat.Size) ? 
                (stat.Size % BlockSize) : 
                BlockSize;
            await fs.WriteAsync(what, 0, size);
        }
        private void InitNewFile()
        {
            var stat = fileStats[fileStats.Count - 1];
            FileStream file = new FileStream(stat.Name, FileMode.Create);
            fileStreams.Add(file);
        }
        private void CloseFile(int ptr)
        {
            fileStreams[ptr].Close();
        }
    }
    class TransportManager
    {
        const int uniPort = 4000;

    }
    class DownloadManager
    {

    }
    class UploadManager
    {
    }
    class Client
    {
    }
}
