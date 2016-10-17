﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace ReClassNET.DataExchange
{
	partial class ReClassFile : IReClassImport
	{
		public const string FormatName = "ReClass File";
		public const string FileExtension = ".reclass";

		private Dictionary<string, SchemaClassNode> classes;

		public SchemaBuilder Load(string filePath, ReportError report)
		{
			try
			{
				var document = XDocument.Load(filePath);

				var typeMap = TypeMap2013;

				var versionComment = document.Root.FirstNode as XComment;
				if (versionComment != null)
				{
					switch (versionComment.Value.Substring(0, 12).ToLower())
					{
						case "reclass 2011":
						case "reclass 2013":
							break;
						case "reclass 2015":
						case "reclass 2016":
							typeMap = TypeMap2016;
							break;
					}
				}

				classes = document.Root
					.Elements("Class")
					.ToDictionary(
						cls => cls.Attribute("Name")?.Value,
						cls =>
						{
							long address;
							long.TryParse(cls.Attribute("Offset")?.Value, out address);
							return new SchemaClassNode
							{
#if WIN32
								Offset = unchecked((IntPtr)(int)address),
#else
								Offset = unchecked((IntPtr)address),
#endif

								Name = cls.Attribute("Name")?.Value
							};
						}
					);

				var schema = document.Root
					.Elements("Class")
					.Select(cls => new { Data = cls, Class = classes[cls.Attribute("Name")?.Value] })
					.Select(x =>
					{
						x.Class.Nodes.AddRange(x.Data.Elements("Node").Select(n => ParseNode(n, typeMap, report)).Where(n => n != null));
						return x.Class;
					});

				return SchemaBuilder.FromSchema(schema);
			}
			catch (Exception ex)
			{
				report?.Invoke(ex.Message);

				return null;
			}
		}

		private SchemaNode ParseNode(XElement node, SchemaType[] typeMap, ReportError report)
		{
			var type = SchemaType.None;

			int typeVal;
			if (int.TryParse(node.Attribute("Type")?.Value, out typeVal))
			{
				if (typeVal >= 0 && typeVal < typeMap.Length)
				{
					type = typeMap[typeVal];
				}
			}

			if (type == SchemaType.None)
			{
				report?.Invoke($"Node has unknown type: " + node.ToString());

				return null;
			}

			SchemaNode sn = null;

			if (type == SchemaType.Array || type == SchemaType.ClassPtrArray || type == SchemaType.ClassInstance || type == SchemaType.ClassPtr)
			{
				string reference = null;
				if (type == SchemaType.Array)
				{
					reference = node.Element("Array")?.Attribute("Name")?.Value;
				}
				else
				{
					reference = node.Attribute("Pointer")?.Value ?? node.Attribute("Instance")?.Value;
				}

				if (reference == null || !classes.ContainsKey(reference))
				{
					report?.Invoke("Can't resolve referenced class: " + node.ToString());

					return null;
				}

				sn = new SchemaReferenceNode(type, classes[reference]);
			}
			else if (type == SchemaType.VTable)
			{
				var vtableNode = new SchemaVTableNode();
				vtableNode.Nodes.AddRange(node.Elements("Function").Select(f => new SchemaNode(SchemaType.VMethod)
				{
					Name = f.Attribute("Name")?.Value,
					Comment = f.Attribute("Comment")?.Value
				}));
				sn = vtableNode;
			}
			else
			{
				sn = new SchemaNode(type);
			}

			sn.Name = node.Attribute("Name")?.Value;
			sn.Comment = node.Attribute("Comment")?.Value;

			switch (type)
			{
				case SchemaType.Array:
					int count;
					int.TryParse(node.Attribute("Total")?.Value, out count);
					sn.Count = count;
					break;
				case SchemaType.ClassPtrArray:
					int size;
					int.TryParse(node.Attribute("Size")?.Value, out size);
					sn.Count = size / IntPtr.Size;
					break;
				case SchemaType.UTF8Text:
				case SchemaType.UTF16Text:
					int length;
					int.TryParse(node.Attribute("Size")?.Value, out length);
					sn.Count = type == SchemaType.UTF8Text ? length : length / 2;
					break;
				case SchemaType.Custom:
					int size2;
					int.TryParse(node.Attribute("Size")?.Value, out size2);
					sn.Count = size2;
					break;
			}

			return sn;
		}

		#region ReClass 2011 / ReClass 2013

		private static readonly SchemaType[] TypeMap2013 = new SchemaType[]
		{
			SchemaType.None,
			SchemaType.ClassInstance,
			SchemaType.None,
			SchemaType.None,
			SchemaType.Hex32,
			SchemaType.Hex16,
			SchemaType.Hex8,
			SchemaType.ClassPtr,
			SchemaType.Int32,
			SchemaType.Int16,
			SchemaType.Int8,
			SchemaType.Float,
			SchemaType.UInt32,
			SchemaType.UInt16,
			SchemaType.UInt8,
			SchemaType.UTF8Text,
			SchemaType.FunctionPtr,
			SchemaType.Custom,
			SchemaType.Vector2,
			SchemaType.Vector3,
			SchemaType.Vector4,
			SchemaType.Matrix4x4,
			SchemaType.VTable,
			SchemaType.Array,
			SchemaType.Class,
			SchemaType.None,
			SchemaType.None,
			SchemaType.Int64,
			SchemaType.Double,
			SchemaType.UTF16Text,
			SchemaType.ClassPtrArray
		};

		#endregion

		#region ReClass 2015 / ReClass 2016

		private static readonly SchemaType[] TypeMap2016 = new SchemaType[]
		{
			SchemaType.None,
			SchemaType.ClassInstance,
			SchemaType.None,
			SchemaType.None,
			SchemaType.Hex32,
			SchemaType.Hex64,
			SchemaType.Hex16,
			SchemaType.Hex8,
			SchemaType.ClassPtr,
			SchemaType.Int64,
			SchemaType.Int32,
			SchemaType.Int16,
			SchemaType.Int8,
			SchemaType.Float,
			SchemaType.Double,
			SchemaType.UInt32,
			SchemaType.UInt16,
			SchemaType.UInt8,
			SchemaType.UTF8Text,
			SchemaType.UTF16Text,
			SchemaType.FunctionPtr,
			SchemaType.Custom,
			SchemaType.Vector2,
			SchemaType.Vector3,
			SchemaType.Vector4,
			SchemaType.Matrix4x4,
			SchemaType.VTable,
			SchemaType.Array,
			SchemaType.Class,
			SchemaType.UTF8TextPtr,
			SchemaType.UTF16TextPtr,
			SchemaType.BitMap,
			SchemaType.UInt64
		};

		#endregion
	}
}
