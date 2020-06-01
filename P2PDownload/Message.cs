using System;
using System.Net;
using System.Security.Cryptography;
using System.IO;
using BinarySerialization;
using System.Diagnostics;
using System.Collections.Generic;
using log4net;

namespace Toy
{
    enum P2PMessageType 
    {
        GET_DATA,
        GET_FILE_INFO,
        GET_BLOCK_INFO,
        DATA,
        ERROR,
        FILE_INFO,
        BLOCK_INFO,
        GET_OK,
        CLOSE,
        ALIVE
    }
    class P2PMessage
    {
        [FieldOrder(0)]
        public P2PMessageType type;
        [FieldOrder(1)]
        public Int32 length { get; set; }
        public P2PMessage() { }
        public P2PMessage(P2PMessageType type_value, int length_value) {
            type = type_value;
            length = length_value;
        }
        static public P2PMessage GenFromBuffer(byte[] buffer, int size)
        {
            return new P2PMessage();
        }
        static public P2PMessage GenFromSocket(System.Net.Sockets.NetworkStream stream, int type_value, int length)
        {
            P2PMessage ret;
            var buffer = new byte[length];
            stream.Read(buffer, 0, length);
            switch (type_value)
            {
                case 0:
                    ret = new GetDataMessage(buffer);
                    break;
                case 1:
                    ret = new GetFileInfoMessage(buffer);
                    break;
                case 2:
                    ret = new GetBlockInfoMessage(buffer);
                    break;
                case 3:
                    ret = new DataMessage(buffer);
                    break;
                case 4:
                    ret = new ErrorMessage(buffer);
                    break;
                case 5:
                    ret = new FileInfoMessage(buffer);
                    break;
                case 6:
                    ret = new BlockInfoMessage(buffer);
                    break;
                case 7:
                    ret = new OKMessage(buffer);
                    break;
                case 8:
                    ret = new CloseMessage();
                    break;
                default:
                    throw new System.Exception("not allow");
            }
            return ret;
        }
        static public List<P2PMessage> GenFromMS(MemoryStream ms)
        {
            var now_pos = 0;
            List<P2PMessage> ret = new List<P2PMessage>();
            var max_pos = ms.Position;
            ms.Position = 0;
            while (now_pos < max_pos) {
                var buffer_head = new byte[8];
                ms.Read(buffer_head, 0, 8);
                var length = BitConverter.ToInt32(buffer_head, 4);
                var type_value = BitConverter.ToInt32(buffer_head);
                var buffer_tail = new byte[length];
                ms.Read(buffer_tail, 0, length);
                var now_item = new P2PMessage();
                switch (type_value)
                {
                    case 0:
                        now_item = new GetDataMessage(buffer_tail);
                        break;
                    case 1:
                        now_item = new GetFileInfoMessage(buffer_tail);
                        break;
                    case 2:
                        now_item = new GetBlockInfoMessage(buffer_tail);
                        break;
                    case 3:
                        now_item =  new DataMessage(buffer_tail);
                        break;
                    case 4:
                        now_item = new ErrorMessage(buffer_tail);
                        break;
                    case 5:
                        now_item =  new FileInfoMessage(buffer_tail);
                        break;
                    case 6:
                        now_item = new BlockInfoMessage(buffer_tail);
                        break;
                    case 7:
                        now_item = new OKMessage(buffer_tail);
                        break;
                    case 8:
                        now_item = new CloseMessage();
                        break;
                    case 9:
                        now_item = new AliveMessage();
                        break;
                    default:
                        throw new System.Exception("not allow");
                }
                now_item.type = (P2PMessageType)type_value;
                ret.Add(now_item);
                now_pos += 8 + length;
            }
            Debug.Assert(now_pos == max_pos);
            return ret;
        }
        static public int TryParseHeader(byte[] header)
        {
            return BitConverter.ToInt32(header, 4);
        }
    }
    class GetDataMessage : P2PMessage
    {
        [FieldAlignment(4)]
        [FieldOrder(0)]
        public Int32[] what;
        [FieldOrder(1)]
        public byte[] hash;
        public GetDataMessage(byte[] hash_value, Int32[] indexs)
        {
            hash = hash_value;
            type = P2PMessageType.GET_DATA;
            what = indexs;
            length = what.Length * sizeof(int) + 32;
        }
        public GetDataMessage(byte[] buffer)
        {
            type = P2PMessageType.GET_DATA;
            hash = new byte[32];
            var count = (buffer.Length - 32) / 4;
            what = new int[count];
            for(var i = 0; i < count; ++i)
            {
                what[i] = BitConverter.ToInt32(buffer, i * 4);
            }
            Array.Copy(buffer, buffer.Length - 32, hash, 0, 32);
        }
    }
    class GetFileInfoMessage : P2PMessage
    {
        [FieldLength(nameof(length))]
        public Byte[] hash;
        public GetFileInfoMessage(Byte[] hash_value)
        {
            if(hash_value.Length != 32)
            {
                throw new InvalidDataException("hash_value must be 32byte");
            }
            type = P2PMessageType.GET_FILE_INFO;
            hash = hash_value;
            length = hash_value.Length;
        }
    }
    class BlockInfo
    {
        [FieldOrder(1)]
        public Byte[] hashOfFile;
        [FieldOrder(0)]
        public Int32 index;
        public BlockInfo(Byte[] hash, Int32 index_value)
        {
            hashOfFile = hash;
            index = index_value;
        }
        static public int GetSize()
        {
            return 4 + 32;
        }
        public BlockInfo(byte[] buffer)
        {
            hashOfFile = new byte[32];
            Array.Copy(buffer, 4, hashOfFile, 0, 32);
            index = BitConverter.ToInt32(buffer, 0);
        }
    }
    class GetBlockInfoMessage : P2PMessage
    {
        public BlockInfo BlockInfo;
        public GetBlockInfoMessage(Byte[] hash_of_file, Int32 index_value)
        {
            type = P2PMessageType.GET_BLOCK_INFO;
            BlockInfo = new BlockInfo(hash_of_file, index_value);
            length = BlockInfo.GetSize();
        }
        public GetBlockInfoMessage(byte[] buffer)
        {
            type = P2PMessageType.GET_BLOCK_INFO;
            BlockInfo = new BlockInfo(buffer);
        }

    }
    class P2PData
    {
        [FieldOrder(0)]
        public BlockInfo block;
        [FieldOrder(1)]
        public Byte[] data; 
        public P2PData(BlockInfo block_value, Byte[] data_value)
        {
            block = block_value;
            data = data_value;
        }
        public P2PData(byte[] data_value, byte[] hash, Int32 index)
        {
            block = new BlockInfo(hash, index);
            data = data_value;
        }
        public P2PData(byte[] buffer)
        {
            var i = new byte[BlockInfo.GetSize()];
            Array.Copy(buffer, 0, i, 0, BlockInfo.GetSize());
            block = new BlockInfo(i);
            data = new byte[4096];
            Array.Copy(buffer, 36, data, 0, 4096);
        }
        static public int GetSize()
        {
            return BlockInfo.GetSize() + 4096;
        }
    }
    class DataMessage : P2PMessage
    {
        [FieldOrder(0)]
        public P2PData p2PDatas;
        public DataMessage(byte[] data, byte[] hash, Int32 index)
        {
            type = P2PMessageType.DATA;
            p2PDatas = new P2PData(data, hash, index);
            length = P2PData.GetSize();
        }
        public DataMessage(byte[] buffer)
        {
            p2PDatas = new P2PData(buffer);
            type = P2PMessageType.DATA;
        }
    }
    class ErrorMessage : P2PMessage
    {
        public BlockInfo Block;
        public ErrorMessage(byte[]hash, Int32 index_value)
        {
            Block = new BlockInfo(hash, index_value);
            type = P2PMessageType.ERROR;
            length = BlockInfo.GetSize();
        }
        public ErrorMessage(byte[] buffer)
        {
            Block = new BlockInfo(buffer);
        }
    }
    class FileLocateInfo
    {
        [FieldOrder(0)]
        public byte[] locate;
        [FieldOrder(1)]
        public Int32 count;
        [FieldOrder(2)]
        public Int32[] indexs;
        [FieldOrder(3)]
        public byte[] hash;
        public FileLocateInfo(IPAddress locate_value, byte[] hash_value, Int32[] indexs_value)
        {
            locate = locate_value.GetAddressBytes();
            indexs = indexs_value;
            count = indexs.Length;
            hash = hash_value;
        }
        public FileLocateInfo(byte[] locate_value, byte[] hash_value,
            Int32 count_value, Int32[] indexs_value)
        {
            locate = locate_value;
            count = count_value;
            indexs = indexs_value;
            hash = hash_value;
        }
        public int GetSize()
        {
            return 4 + 32 + 4 + indexs.Length * 4;
        }
    }
    class FileInfoMessage : P2PMessage
    {
        [FieldOrder(0)]
        public int count;
        [FieldOrder(1)]
        public FileLocateInfo[] locateInfos;
        static ILog _logger = Logger.GetLogger(typeof(FileInfoMessage));
        public FileInfoMessage(FileLocateInfo[] fileLocateInfos)
        {
            type = P2PMessageType.FILE_INFO;
            locateInfos = fileLocateInfos;
            length = 4;
            foreach(var i in locateInfos)
            {
                length += i.GetSize();
            }
            count = fileLocateInfos.Length;
        }
        public FileInfoMessage(byte[] buffer)
        {
            type = P2PMessageType.FILE_INFO;
            var index_now = 4;
            count = BitConverter.ToInt32(buffer, 0);
            locateInfos = new FileLocateInfo[count];
            int l_count = 0;
            while(index_now < buffer.Length)
            {
                var locate = new byte[4];
                Array.Copy(buffer, 0 + index_now, locate, 0, 4);
                index_now += 4;
                var countOfIndexs = 0;
                countOfIndexs = BitConverter.ToInt32(buffer, index_now);
                index_now += 4;
                var indexs = new Int32[countOfIndexs];
                var i = 0;
                for(; i < countOfIndexs; ++i)
                {
                    indexs[i] = BitConverter.ToInt32(buffer, index_now);
                    index_now += 4;
                }
                var hash = new byte[32];
                _logger.Info($"i is {i}, countOfIndexs is {countOfIndexs}, index_now is {index_now}");
                Array.Copy(buffer, index_now, hash, 0, 32);
                
                index_now += 32;
                locateInfos[l_count] = new FileLocateInfo(locate, hash, countOfIndexs, indexs);
                l_count++;
            }
            length = index_now;
            Debug.Assert(index_now == buffer.Length);
        }
    }
    class BlockInfoMessage : P2PMessage
    {
        public BlockInfo block;
        public BlockInfoMessage(byte[] hash, Int32 index_value)
        {
            type = P2PMessageType.BLOCK_INFO;
            block = new BlockInfo(hash, index_value);
            length = BlockInfo.GetSize();
        }
        public BlockInfoMessage(byte[] buffer)
        {
            type = P2PMessageType.BLOCK_INFO;
            length = buffer.Length;
            block = new BlockInfo(buffer);
        }
    }
    class OKMessage : P2PMessage
    {
        public BlockInfo block;
        public OKMessage(byte[] hash, Int32 index_value)
        {
            block = new BlockInfo(hash, index_value);
            length = BlockInfo.GetSize();
            type = P2PMessageType.GET_OK;
        }
        public OKMessage(byte[] buffer)
        {
            block = new BlockInfo(buffer);
        }
    }

    class CloseMessage : P2PMessage
    {
        public CloseMessage()
        {
            type = P2PMessageType.CLOSE;
            length = 0;
        }
    }

    class AliveMessage : P2PMessage {
        public AliveMessage()
        {
            type = P2PMessageType.ALIVE;
            length = 0;
        }
    }
    
    class Message
    {
        static void WriteToFile(string path, byte[] what)
        {
            if (!File.Exists(path))
            {
                // Create a file to write to.
                using (var sw = File.Create(path))
                {
                    sw.Write(what, 0, what.Length);
                }
            }

            using (var sr = File.OpenWrite(path))
            {
                sr.Write(what, 0, what.Length);
            }
        }
        static void serializeAndWrite(P2PMessage p2PMessage)
        {
            string path = @"C:\Users\ayanamists\source\repos\P2PDownload\P2PDownload\test";
            var stream = new MemoryStream();
            var serializer = new BinarySerializer();
            serializer.Serialize(stream, p2PMessage);
            WriteToFile($"{path}\\{p2PMessage.type}", stream.ToArray());
        }
        static Tuple<byte[],int> ReadFromFile(string path)
        {
            byte[] ret = new byte[5000];
            var size = 0;
            using(var sr = File.OpenRead(path))
            {
                size = sr.Read(ret, 0, 5000);
            }
            return new Tuple<byte[], int>(ret, size);
        }
        static void TestParse(P2PMessage p2PMessage)
        {
            string path = @"C:\Users\ayanamists\source\repos\P2PDownload\P2PDownload\test";
            var i = ReadFromFile($"{path}\\{p2PMessage.type}");
            var r = P2PMessage.GenFromBuffer(i.Item1, i.Item2);
            var stream = new MemoryStream();
            var serializer = new BinarySerializer();
            serializer.Serialize(stream, p2PMessage);
            WriteToFile($"{path}\\{p2PMessage.type}-2", stream.ToArray());
        }
        public static void Test(string[] args)
        {
            Int32[] temp = { 1, 2, 3, 4};
            var hash = SHA256.Create().ComputeHash(new byte[1]);
            GetDataMessage getData = new GetDataMessage(hash, temp);
            serializeAndWrite(getData);
            TestParse(getData);

            GetFileInfoMessage getFileInfo = new GetFileInfoMessage(hash);
            serializeAndWrite(getFileInfo);
            TestParse(getFileInfo);

            GetBlockInfoMessage getBlockInfo = new GetBlockInfoMessage(hash, 10);
            serializeAndWrite(getBlockInfo);
            TestParse(getBlockInfo);

            var block = new byte[4096];
            block[1] = 0; block[2] = 2;
            DataMessage data = new DataMessage(block, hash, 10);
            serializeAndWrite(data);
            TestParse(data);

            var error = new ErrorMessage(hash, 10);
            serializeAndWrite(error);
            TestParse(error);

            IPAddress a = IPAddress.Parse("192.168.1.190");
            int[] array = { 1, 3, 4 };
            FileLocateInfo fileLocateInfo = new FileLocateInfo(a, hash, array);
            FileLocateInfo[] fileLocateInfos = new FileLocateInfo[1];
            fileLocateInfos[0] = fileLocateInfo;
            var fileInfo = new FileInfoMessage(fileLocateInfos);
            serializeAndWrite(fileInfo);
            TestParse(fileInfo);

            BlockInfoMessage blockInfo = new BlockInfoMessage(hash, 10);
            serializeAndWrite(blockInfo);
            TestParse(blockInfo);

            OKMessage oK = new OKMessage(hash, 10);
            serializeAndWrite(oK);
            TestParse(oK);
        }
    }
}
