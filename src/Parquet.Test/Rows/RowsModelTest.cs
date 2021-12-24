﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Parquet.Data;
using Parquet.Data.Rows;
using Xunit;

namespace Parquet.Test.Rows
{
   public class RowsModelTest : TestBase
   {
      #region [ Flat Tables ]

      [Fact]
      public async Task Flat_add_valid_row_succeeds()
      {
         var table = new Table(new Schema(new DataField<int>("id")));
         table.Add(new Row(1));
      }

      [Fact]
      public async Task Flat_add_invalid_type_fails()
      {
         var table = new Table(new Schema(new DataField<int>("id")));

         Assert.Throws<ArgumentException>(() => table.Add(new Row("1")));
      }

      [Fact]
      public async Task Flat_write_read()
      {
         var table = new Table(new Schema(new DataField<int>("id"), new DataField<string>("city")));
         var ms = new MemoryStream();

         //generate fake data
         for (int i = 0; i < 1000; i++)
         {
            table.Add(new Row(i, "record#" + i));
         }

         //write to stream
         using (ParquetWriter writer = await ParquetWriter.Open(table.Schema, ms))
         {
            await writer.Write(table);
         }

         //read back into table
         ms.Position = 0;
         Table table2;
         using (ParquetReader reader = await ParquetReader.Open(ms))
         {
            table2 = await reader.ReadAsTable();
         }

         //validate data
         Assert.True(table.Equals(table2, true));
      }

      [Fact]
      public async Task Flat_concurrent_write_read()
      {
         var schema = new Schema(
            new DataField<int>("id"),
            new DataField<string>("city"));

         var inputTables = new List<Table>();
         for (int i = 0; i < 10; i++)
         {
            var table = new Table(schema);
            for (int j = 0; j < 10; j++)
            {
               table.Add(new Row(i, $"record#{i},{j}"));
            }

            inputTables.Add(table);
         }

         Task<Table>[] tasks = inputTables
               .Select(t => Task.Run(() => WriteRead(t)))
               .ToArray();

         Table[] outputTables = await Task.WhenAll(tasks);

         for (int i = 0; i < inputTables.Count; i++)
         {
            Assert.True(inputTables[i].Equals(outputTables[i], true));
         }
      }

      #endregion

      #region [ Array Tables ]

      [Fact]
      public async Task Array_validate_succeeds()
      {
         var table = new Table(new Schema(new DataField<IEnumerable<int>>("ids")));

         table.Add(new Row(new[] { 1, 2, 3 }));
         table.Add(new Row(new[] { 4, 5, 6 }));
      }

      [Fact]
      public async Task Array_validate_fails()
      {
         var table = new Table(new Schema(new DataField<IEnumerable<int>>("ids")));

         Assert.Throws<ArgumentException>(() => table.Add(new Row(1)));
      }

      [Fact]
      public async Task Array_write_read()
      {
         var table = new Table(
            new Schema(
               new DataField<int>("id"),
               new DataField<string[]>("categories")     //array field
               )
            );
         var ms = new MemoryStream();

         table.Add(1, new[] { "1", "2", "3" });
         table.Add(3, new[] { "3", "3", "3" });

         //write to stream
         using (ParquetWriter writer = await ParquetWriter.Open(table.Schema, ms))
         {
            await writer.Write(table);
         }

         //System.IO.File.WriteAllBytes("c:\\tmp\\1.parquet", ms.ToArray());

         //read back into table
         ms.Position = 0;
         Table table2;
         using (ParquetReader reader = await ParquetReader.Open(ms))
         {
            table2 = await reader.ReadAsTable();
         }

         //validate data
         Assert.Equal(table.ToString(), table2.ToString(), ignoreLineEndingDifferences: true);
      }

      #endregion

      #region [ Maps ]

      [Fact]
      public async Task Map_validate_succeeds()
      {
         var table = new Table(new Schema(
            new MapField("map", new DataField<string>("key"), new DataField<string>("value"))
            ));

         table.Add(Row.SingleCell(
            new List<Row>
            {
               new Row("one", "v1"),
               new Row("two", "v2")
            }));
      }

      [Fact]
      public async Task Map_validate_fails()
      {
         var table = new Table(new Schema(
            new MapField("map", new DataField<string>("key"), new DataField<string>("value"))
            ));

         Assert.Throws<ArgumentException>(() => table.Add(new Row(1)));
      }

      [Fact]
      public async Task Map_read_from_Apache_Spark()
      {
         Table t;
         using (Stream stream = OpenTestFile("map_simple.parquet"))
         {
            using (ParquetReader reader = await ParquetReader.Open(stream))
            {
               t = await reader.ReadAsTable();
            }
         }

         Assert.Equal("{'id': 1, 'numbers': [{'key': 1, 'value': 'one'}, {'key': 2, 'value': 'two'}, {'key': 3, 'value': 'three'}]}", t[0].ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task Map_write_read()
      {
         var table = new Table(
            new Schema(
               new DataField<string>("city"),
               new MapField("population",
                  new DataField<int>("areaId"),
                  new DataField<long>("count"))));
         var ms = new MemoryStream();

         table.Add("London",
            new List<Row>
            {
               new Row(234, 100L),
               new Row(235, 110L)
            });

         Table table2 = await WriteRead(table);

         Assert.Equal(table.ToString(), table2.ToString(), ignoreLineEndingDifferences: true);
      }

      #endregion

      #region [ Struct ]

      [Fact]
      public async Task Struct_read_plain_structs_from_Apache_Spark()
      {
         Table t = await ReadTestFileAsTable("struct_plain.parquet");

         Assert.Equal(@"{'isbn': '12345-6', 'author': {'firstName': 'Ivan', 'lastName': 'Gavryliuk'}}
{'isbn': '12345-7', 'author': {'firstName': 'Richard', 'lastName': 'Conway'}}", t.ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task Struct_write_read()
      {
         var table = new Table(
            new Schema(
               new DataField<string>("isbn"),
               new StructField("author",
                  new DataField<string>("firstName"),
                  new DataField<string>("lastName"))));
         var ms = new MemoryStream();

         table.Add("12345-6", new Row("Ivan", "Gavryliuk"));
         table.Add("12345-7", new Row("Richard", "Conway"));

         Table table2 = await WriteRead(table);

         Assert.Equal(table.ToString(), table2.ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task Struct_with_repeated_field_writes_reads()
      {
         var t = new Table(new Schema(
            new DataField<string>("name"),
            new StructField("address",
               new DataField<string>("name"),
               new DataField<IEnumerable<string>>("lines"))));

         t.Add("Ivan", new Row("Primary", new[] { "line1", "line2" }));

         Table t2 = await WriteRead(t);

         Assert.Equal("{'name': 'Ivan', 'address': {'name': 'Primary', 'lines': ['line1', 'line2']}}", t2.ToString(), ignoreLineEndingDifferences: true);
      }

      #endregion

      #region [ List ]

      [Fact]
      public void List_table_equality()
      {
         var schema = new Schema(new ListField("ints", new DataField<int>("int")));


         var tbl1 = new Table(schema);
         tbl1.Add(Row.SingleCell(new[] { 1, 1, 1 }));

         var tbl2 = new Table(schema);
         tbl2.Add(Row.SingleCell(new[] { 1, 1, 1 }));

         Assert.True(tbl1.Equals(tbl2, true));
      }

      [Fact]
      public async Task List_read_simple_element_from_Apache_Spark()
      {
         Table t;
         using (Stream stream = OpenTestFile("list_simple.parquet"))
         {
            using (ParquetReader reader = await ParquetReader.Open(stream))
            {
               t = await reader.ReadAsTable();
            }
         }

         Assert.Equal("{'cities': ['London', 'Derby', 'Paris', 'New York'], 'id': 1}", t[0].ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_simple_element_write_read()
      {
         var table = new Table(
            new Schema(
               new DataField<int>("id"),
               new ListField("cities",
                  new DataField<string>("name"))));

         var ms = new MemoryStream();

         table.Add(1, new[] { "London", "Derby" });
         table.Add(2, new[] { "Paris", "New York" });

         //write as table
         using (ParquetWriter writer = await ParquetWriter.Open(table.Schema, ms))
         {
            await writer.Write(table);
         }

         //read back into table
         ms.Position = 0;
         Table table2;
         using (ParquetReader reader = await ParquetReader.Open(ms))
         {
            table2 = await reader.ReadAsTable();
         }

         //validate data
         Assert.Equal(table.ToString(), table2.ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_read_structures_from_Apache_Spark()
      {
         Table t;
         using (Stream stream = OpenTestFile("list_structs.parquet"))
         {
            using (ParquetReader reader = await ParquetReader.Open(stream))
            {
               t = await reader.ReadAsTable();
            }
         }

         Assert.Single(t);
         Assert.Equal("{'cities': [{'country': 'UK', 'name': 'London'}, {'country': 'US', 'name': 'New York'}], 'id': 1}", t[0].ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_read_write_structures()
      {
         Table t = new Table(
            new DataField<int>("id"),
            new ListField("structs",
               new StructField("mystruct",
                  new DataField<int>("id"),
                  new DataField<string>("name"))));

         t.Add(1, new[] { new Row(1, "Joe"), new Row(2, "Bloggs") });
         t.Add(2, new[] { new Row(3, "Star"), new Row(4, "Wars") });

         Table t2 = await WriteRead(t);

         Assert.Equal(t.ToString(), t2.ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_of_elements_is_empty_read_from_Apache_Spark()
      {
         Table t = await ReadTestFileAsTable("list_empty.parquet");

         Assert.Equal("{'id': 2, 'repeats1': []}", t[0].ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_of_elements_empty_alternates_read_from_Apache_Spark()
      {
         /*
          list data:
          - 1: [1, 2, 3]
          - 2: []
          - 3: [1, 2, 3]
          - 4: []
          */

         Table t = await ReadTestFileAsTable("list_empty_alt.parquet");

         Assert.Equal(@"{'id': 1, 'repeats2': ['1', '2', '3']}
{'id': 2, 'repeats2': []}
{'id': 3, 'repeats2': ['1', '2', '3']}
{'id': 4, 'repeats2': []}", t.ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_of_elements_is_empty_writes_reads()
      {
         var t = new Table(
            new DataField<int>("id"),
            new ListField("strings",
               new DataField<string>("item")
            ));
         t.Add(1, new string[0]);
         Assert.Equal("{'id': 1, 'strings': []}", (await WriteRead(t)).ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_of_elements_with_some_items_empty_writes_reads()
      {
         var t = new Table(
            new DataField<int>("id"),
            new ListField("strings",
               new DataField<string>("item")
            ));
         t.Add(1, new string[] { "1", "2", "3" });
         t.Add(2, new string[] { });
         t.Add(3, new string[] { "1", "2", "3" });
         t.Add(4, new string[] { });

         Table t1 = await WriteRead(t);
         Assert.Equal(4, t1.Count);
         Assert.Equal(@"{'id': 1, 'strings': ['1', '2', '3']}
{'id': 2, 'strings': []}
{'id': 3, 'strings': ['1', '2', '3']}
{'id': 4, 'strings': []}", t.ToString(), ignoreLineEndingDifferences: true);

      }

      [Fact]
      public async Task List_of_lists_read_write_structures()
      {
         var t = new Table(
             new DataField<int>("id"),
             new ListField(
                 "items",
                 new StructField(
                     "item",
                     new DataField<int>("id"),
                     new ListField(
                         "values",
                         new DataField<string>("value")))));

         t.Add(0, new[]
         {
            new Row(0, new[] { "0,0,0", "0,0,1", "0,0,2" }),
            new Row(1, new[] { "0,1,0", "0,1,1", "0,1,2" }),
            new Row(2, new[] { "0,2,0", "0,2,1", "0,2,2" }),
         });

         Table t1 = await WriteRead(t);
         Assert.Equal(3, ((object[])t1[0][1]).Length);
         Assert.Equal(t.ToString(), t1.ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_of_structures_is_empty_read_write_structures()
      {
         Table t = new Table(
            new DataField<int>("id"),
            new ListField("structs",
               new StructField("mystruct",
                  new DataField<int>("id"),
                  new DataField<string>("name"))));

         t.Add(1, new Row[0]);
         t.Add(2, new Row[0]);

         Table t2 = await WriteRead(t);

         Assert.Equal(t.ToString(), t2.ToString(), ignoreLineEndingDifferences: true);
      }

      [Fact]
      public async Task List_of_structures_is_empty_as_first_field_read_write_structures()
      {
         Table t = new Table(
            new ListField("structs",
               new StructField("mystruct",
                  new DataField<int>("id"),
                  new DataField<string>("name"))),
            new DataField<int>("id"));

         t.Add(new Row[0], 1);
         t.Add(new Row[0], 2);

         Table t2 = await WriteRead(t);

         Assert.Equal(t.ToString(), t2.ToString(), ignoreLineEndingDifferences: true);
      }

      #endregion

      #region [ Mixed ]

      #endregion

      #region [ Special Cases ]

      [Fact]
      public async Task Special_read_all_nulls_no_booleans()
      {
         ReadTestFileAsTable("special/all_nulls_no_booleans.parquet");
      }

      [Fact]
      public async Task Special_all_nulls_file()
      {
         Table t = await ReadTestFileAsTable("special/all_nulls.parquet");

         Assert.Equal(1, t.Schema.Fields.Count);
         Assert.Equal("lognumber", t.Schema[0].Name);
         Assert.Single(t);
         Assert.Null(t[0][0]);
      }

      [Fact]
      public async Task Special_read_all_nulls_decimal_column()
      {
         await ReadTestFileAsTable("special/decimalnulls.parquet");
      }

      [Fact]
      public async Task Special_read_all_legacy_decimals()
      {
         Table ds = await ReadTestFileAsTable("special/decimallegacy.parquet");

         Row row = ds[0];
         Assert.Equal(1, (int)row[0]);
         Assert.Equal(1.2m, (decimal)row[1], 2);
         Assert.Null(row[2]);
         Assert.Equal(-1m, (decimal)row[3], 2);
      }

      [Fact]
      public async Task Special_read_file_with_multiple_row_groups()
      {
         var ms = new MemoryStream();

         //create multirowgroup file

         //first row group
         var t = new Table(new DataField<int>("id"));
         t.Add(1);
         t.Add(2);
         using (ParquetWriter writer = await ParquetWriter.Open(t.Schema, ms))
         {
            await writer.Write(t);
         }

         //second row group
         t.Clear();
         t.Add(3);
         t.Add(4);
         using (ParquetWriter writer = await ParquetWriter.Open(t.Schema, ms, null, true))
         {
            await writer.Write(t);
         }

         //read back as table
         t = await ParquetReader.ReadTableFromStream(ms);
         Assert.Equal(4, t.Count);
      }

      #endregion

      #region [ And the Big Ultimate Fat Test!!! ]

      /// <summary>
      /// This essentially proves that we can READ complicated data structures, but not write (see other tests)
      /// </summary>
      [Fact]
      public async Task BigFatOne_variations_from_Apache_Spark()
      {
         Table t;
         using (Stream stream = OpenTestFile("all_var1.parquet"))
         {
            using (ParquetReader reader = await ParquetReader.Open(stream))
            {
               t = await reader.ReadAsTable();
            }
         }

         Assert.Equal(2, t.Count);
         Assert.Equal("{'addresses': [{'line1': 'Dante Road', 'name': 'Head Office', 'openingHours': [9, 10, 11, 12, 13, 14, 15, 16, 17, 18], 'postcode': 'SE11'}, {'line1': 'Somewhere Else', 'name': 'Small Office', 'openingHours': [6, 7, 19, 20, 21, 22, 23], 'postcode': 'TN19'}], 'cities': ['London', 'Derby'], 'comment': 'this file contains all the permunations for nested structures and arrays to test Parquet parser', 'id': 1, 'location': {'latitude': 51.2, 'longitude': 66.3}, 'price': {'lunch': {'max': 2, 'min': 1}}}", t[0].ToString());
         Assert.Equal("{'addresses': [{'line1': 'Dante Road', 'name': 'Head Office', 'openingHours': [9, 10, 11, 12, 13, 14, 15, 16, 17, 18], 'postcode': 'SE11'}, {'line1': 'Somewhere Else', 'name': 'Small Office', 'openingHours': [6, 7, 19, 20, 21, 22, 23], 'postcode': 'TN19'}], 'cities': ['London', 'Derby'], 'comment': 'this file contains all the permunations for nested structures and arrays to test Parquet parser', 'id': 1, 'location': {'latitude': 51.2, 'longitude': 66.3}, 'price': {'lunch': {'max': 2, 'min': 1}}}", t[1].ToString());
      }

      #endregion

      #region [ JSON Conversions ]

      [Fact]
      public async Task JSON_struct_plain_reads_by_newtonsoft()
      {
         Table t = await ReadTestFileAsTable("struct_plain.parquet");

         Assert.Equal("{'isbn': '12345-6', 'author': {'firstName': 'Ivan', 'lastName': 'Gavryliuk'}}", t[0].ToString("jsq"));
         Assert.Equal("{'isbn': '12345-7', 'author': {'firstName': 'Richard', 'lastName': 'Conway'}}", t[1].ToString("jsq"));

         string[] jsons = t.ToString("j").Split(new[] { Environment.NewLine }, StringSplitOptions.None);

         jsons.Select(j => JsonConvert.DeserializeObject(j)).ToList();
      }

      [Fact]
      public async Task JSON_convert_with_null_arrays()
      {
         var t = new Table(new Schema(
            new DataField<string>("name"),
            new ListField("observations",
               new StructField("observation",
                  new DataField<DateTimeOffset>("date"),
                  new DataField<bool>("bool"),
                  new DataField<IEnumerable<int?>>("values")))));

         DateTimeOffset defaultDate = new DateTimeOffset(2018, 1, 1, 1, 1, 1, TimeSpan.Zero);

         t.Add("London", new[]
         {
            new Row(defaultDate, true, new int?[] { 1, 2, 3, 4, 5 })
         });

         t.Add("Oslo", new[]
         {
            new Row(defaultDate, false, new int?[] { null, null, null, null, null, null, null })
         });

         string[] jsons = t.ToString("jsq").Split(new[] {Environment.NewLine}, StringSplitOptions.None);
         Assert.Equal($"{{'name': 'London', 'observations': [{{'date': '{defaultDate}', 'bool': true, 'values': [1, 2, 3, 4, 5]}}]}}", jsons[0]);
         Assert.Equal($"{{'name': 'Oslo', 'observations': [{{'date': '{defaultDate}', 'bool': false, 'values': [null, null, null, null, null, null, null]}}]}}", jsons[1]);
      }

      #endregion
   }
}