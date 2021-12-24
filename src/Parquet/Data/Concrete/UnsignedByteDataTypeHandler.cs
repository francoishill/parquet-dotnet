﻿using System.IO;

namespace Parquet.Data.Concrete
{
   class UnsignedByteDataTypeHandler : BasicPrimitiveDataTypeHandler<byte>
   {
      public UnsignedByteDataTypeHandler() : base(DataType.UnsignedByte, Thrift.Type.INT32, Thrift.ConvertedType.UINT_8)
      {

      }

      protected override byte ReadSingle(BinaryReader reader, Thrift.SchemaElement tse, int length)
      {
         return (byte)reader.ReadInt32();
      }

      protected override void WriteOne(BinaryWriter writer, byte value)
      {
         writer.Write((int)value);
      }
   }
}
