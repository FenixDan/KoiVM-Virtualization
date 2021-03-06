﻿#region

using System.Collections.Generic;
using System.IO;
using System.Text;
using dnlib.DotNet.MD;
using dnlib.IO;
using dnlib.PE;

#endregion

namespace dnlib.DotNet.Writer
{
    /// <summary>
    ///     <see cref="MetaDataHeader" /> options
    /// </summary>
    public sealed class MetaDataHeaderOptions
    {
        /// <summary>
        ///     Default version string
        /// </summary>
        public const string DEFAULT_VERSION_STRING = MDHeaderRuntimeVersion.MS_CLR_20;

        /// <summary>
        ///     Default header signature
        /// </summary>
        public const uint DEFAULT_SIGNATURE = 0x424A5342;

        /// <summary>
        ///     Major version. Default is 1. MS' CLR supports v0.x (x >= 19) and v1.1, nothing else.
        /// </summary>
        public ushort? MajorVersion;

        /// <summary>
        ///     Minor version. Default is 1.
        /// </summary>
        public ushort? MinorVersion;

        /// <summary>
        ///     Reserved and should be 0.
        /// </summary>
        public uint? Reserved1;

        /// <summary>
        ///     Reserved and should be 0
        /// </summary>
        public byte? Reserved2;

        /// <summary>
        ///     MD header signature. Default value is <see cref="DEFAULT_SIGNATURE" />
        /// </summary>
        public uint? Signature;

        /// <summary>
        ///     Storage flags should be 0
        /// </summary>
        public StorageFlags? StorageFlags;

        /// <summary>
        ///     Version string. Default is <see cref="DEFAULT_VERSION_STRING" />. It's stored as a
        ///     zero-terminated UTF-8 string. Length should be &lt;= 255 bytes.
        /// </summary>
        public string VersionString;
    }

    /// <summary>
    ///     Meta data header. IMAGE_COR20_HEADER.MetaData points to this header.
    /// </summary>
    public sealed class MetaDataHeader : IChunk
    {
        private readonly MetaDataHeaderOptions options;
        private uint length;

        /// <summary>
        ///     Default constructor
        /// </summary>
        public MetaDataHeader()
            : this(null)
        {
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="options">Options</param>
        public MetaDataHeader(MetaDataHeaderOptions options)
        {
            this.options = options ?? new MetaDataHeaderOptions();
        }

        /// <summary>
        ///     Gets/sets the heaps
        /// </summary>
        public IList<IHeap> Heaps
        {
            get;
            set;
        }

        /// <inheritdoc />
        public FileOffset FileOffset
        {
            get;
            private set;
        }

        /// <inheritdoc />
        public RVA RVA
        {
            get;
            private set;
        }

        /// <inheritdoc />
        public void SetOffset(FileOffset offset, RVA rva)
        {
            FileOffset = offset;
            RVA = rva;

            length = 16;
            length += (uint) GetVersionString().Length;
            length = Utils.AlignUp(length, 4);
            length += 4;
            foreach(var heap in Heaps)
            {
                length += 8;
                length += (uint) GetAsciizName(heap.Name).Length;
                length = Utils.AlignUp(length, 4);
            }
        }

        /// <inheritdoc />
        public uint GetFileLength()
        {
            return length;
        }

        /// <inheritdoc />
        public uint GetVirtualSize()
        {
            return GetFileLength();
        }

        /// <inheritdoc />
        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(options.Signature ?? MetaDataHeaderOptions.DEFAULT_SIGNATURE);
            writer.Write(options.MajorVersion ?? 1);
            writer.Write(options.MinorVersion ?? 1);
            writer.Write(options.Reserved1 ?? 0);
            var s = GetVersionString();
            writer.Write(Utils.AlignUp(s.Length, 4));
            writer.Write(s);
            writer.WriteZeros(Utils.AlignUp(s.Length, 4) - s.Length);
            writer.Write((byte) (options.StorageFlags ?? 0));
            writer.Write(options.Reserved2 ?? 0);
            writer.Write((ushort) Heaps.Count);
            foreach(var heap in Heaps)
            {
                writer.Write((uint) (heap.FileOffset - FileOffset));
                writer.Write(heap.GetFileLength());
                writer.Write(s = GetAsciizName(heap.Name));
                if(s.Length > 32)
                    throw new ModuleWriterException(string.Format("Heap name '{0}' is > 32 bytes", heap.Name));
                writer.WriteZeros(Utils.AlignUp(s.Length, 4) - s.Length);
            }
        }

        private byte[] GetVersionString()
        {
            return Encoding.UTF8.GetBytes((options.VersionString ?? MetaDataHeaderOptions.DEFAULT_VERSION_STRING) + "\0");
        }

        private byte[] GetAsciizName(string s)
        {
            return Encoding.ASCII.GetBytes(s + "\0");
        }
    }
}