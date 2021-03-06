﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoBuf;

namespace QuantBox.Data.Serializer.V2
{
    public class PbTickSerializer
    {
        private PbTick _lastWrite;
        private PbTick _lastRead;

        public PbTickSerializer()
        {
            Codec = new PbTickCodec();
        }
        public PbTickCodec Codec { get; private set; }

        public void Reset()
        {
            _lastWrite = null;
            _lastRead = null;
        }

        public PbTick Write(PbTick data, Stream dest)
        {
            var diff = Codec.Diff(_lastWrite, data);
            _lastWrite = data;

            diff.PrepareObjectBeforeWrite(Codec.Config);
            ProtoBuf.Serializer.SerializeWithLengthPrefix(dest, diff, PrefixStyle.Base128);

            return diff;
        }

        public void Write(IEnumerable<PbTick> list, Stream stream)
        {
            foreach (var item in list)
            {
                Write(item, stream);
            }
        }

        public void Write(IEnumerable<PbTick> list, string output)
        {
            using (Stream stream = File.Open(output, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                Write(list, stream);
                stream.Close();
            }
        }

        public static void WriteCsv(IEnumerable<PbTick> list, string output)
        {
            if (list == null)
                return;

            var Codec = new PbTickCodec();

            // 将差分数据生成界面数据
            IEnumerable<PbTickView> _list = Codec.Data2View(Codec.Restore(list), false);

            // 保存
            using (TextWriter stream = new StreamWriter(output))
            {
                var t = new PbTickView();
                stream.WriteLine(PbTickView.ToCsvHeader());

                foreach (var l in _list)
                {
                    stream.WriteLine(l);
                }
                stream.Close();
            }
        }

        /// <summary>
        /// 可以同时得到原始的raw和解码后的数据
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public PbTick ReadOne(Stream source, bool unpackDepth = true)
        {
            var raw = ProtoBuf.Serializer.DeserializeWithLengthPrefix<PbTick>(source, PrefixStyle.Base128);
            if (raw == null)
                return null;

            if (unpackDepth)
            {
                raw.PrepareObjectAfterRead(Codec.Config);
            }

            _lastRead = Codec.Restore(_lastRead, raw, unpackDepth);
            if (_lastRead.Config.Version != 2)
            {
                throw new ProtobufDataZeroException("only support pd0 file version 2", _lastRead.Config.Version, 2);
            }
            
            return _lastRead;
        }

        public List<PbTick> Read(Stream stream)
        {
            var _list = new List<PbTick>();

            while (true)
            {
                var resotre = ReadOne(stream);
                if (resotre == null)
                {
                    break;
                }

                _list.Add(resotre);
            }
            stream.Close();

            return _list;
        }

        public List<PbTick> Read(string input)
        {
            using (Stream stream = File.Open(input, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return Read(stream);
            }
        }

    }
}
