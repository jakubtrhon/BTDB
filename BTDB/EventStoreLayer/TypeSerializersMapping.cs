using System;
using System.Collections.Generic;
using BTDB.ODBLayer;
using BTDB.StreamLayer;

namespace BTDB.EventStoreLayer
{
    internal class TypeSerializersMapping : ITypeSerializersMapping
    {
        readonly List<ITypeDescriptor> _id2DescriptorMap = new List<ITypeDescriptor>();
        readonly Dictionary<ITypeDescriptor, int> _descriptor2IdMap = new Dictionary<ITypeDescriptor, int>(ReferenceEqualityComparer<ITypeDescriptor>.Instance);
        readonly TypeSerializers _typeSerializers;

        public TypeSerializersMapping(TypeSerializers typeSerializers)
        {
            _typeSerializers = typeSerializers;
            AddBuildInTypes();
        }

        void AddBuildInTypes()
        {
            _id2DescriptorMap.Add(null);
            foreach (var predefinedType in TypeSerializers.PredefinedTypes)
            {
                _descriptor2IdMap[predefinedType] = _id2DescriptorMap.Count;
                _id2DescriptorMap.Add(predefinedType);
            }
        }

        public void LoadTypeDescriptors(AbstractBufferedReader reader)
        {
            var typeId = reader.ReadVUInt32();
            var typeCategory = (TypeCategory)reader.ReadUInt8();
            switch (typeCategory)
            {
                case TypeCategory.BuildIn:
                    throw new ArgumentOutOfRangeException();
                case TypeCategory.Class:
                    break;
                case TypeCategory.List:
                    break;
                case TypeCategory.Dictionary:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public object LoadObject(AbstractBufferedReader reader)
        {
            var typeId = reader.ReadVUInt32();
            if (typeId == 0)
            {
                return null;
            }
            var descriptor = _id2DescriptorMap[(int)typeId];
            return _typeSerializers.GetLoader(descriptor)(reader);
        }

        public IDescriptorSerializerContext StoreNewDescriptors(AbstractBufferedWriter writer, object obj)
        {
            if (obj == null) return null;
            int typeId;
            var objType = obj.GetType();
            Action<IDescriptorSerializerContext> action;
            DescriptorSerializerContext ctx = null;
            var descriptor = _typeSerializers.DescriptorOf(objType);
            if (_descriptor2IdMap.TryGetValue(descriptor, out typeId))
            {
                action = _typeSerializers.GetNewDescriptorSaver(descriptor);
            }
            else
            {
                ctx = new DescriptorSerializerContext(this);
                ctx.AddDescriptor(descriptor);
                action = _typeSerializers.GetNewDescriptorSaver(descriptor);
            }
            if (action != null)
            {
                if (ctx == null) ctx = new DescriptorSerializerContext(this);
                action(ctx);
            }
            if (ctx != null && ctx.SomeTypeStored)
            {
                foreach (var d2IPair in ctx.Descriptor2IdMap)
                {
                    writer.WriteVUInt32((uint) d2IPair.Value);
                    _typeSerializers.StoreDescriptor(d2IPair.Key, writer, ctx.Descriptor2Id);
                }
                return ctx;
            }
            return null;
        }

        public void CommitNewDescriptors(IDescriptorSerializerContext context)
        {
            if (context == null) return;
            var ctx = (DescriptorSerializerContext)context;
            _id2DescriptorMap.AddRange(ctx.Id2DescriptorMap);
            foreach (var d2IPair in ctx.Descriptor2IdMap)
            {
                _descriptor2IdMap[d2IPair.Key] = d2IPair.Value;
            }
        }

        internal class DescriptorSerializerContext : IDescriptorSerializerContext
        {
            readonly TypeSerializersMapping _typeSerializersMapping;
            internal readonly List<ITypeDescriptor> Id2DescriptorMap = new List<ITypeDescriptor>();
            internal readonly Dictionary<ITypeDescriptor, int> Descriptor2IdMap = new Dictionary<ITypeDescriptor, int>(ReferenceEqualityComparer<ITypeDescriptor>.Instance);

            public DescriptorSerializerContext(TypeSerializersMapping typeSerializersMapping)
            {
                _typeSerializersMapping = typeSerializersMapping;
            }

            public void AddDescriptor(ITypeDescriptor descriptor)
            {
                Descriptor2IdMap.Add(descriptor, _typeSerializersMapping._id2DescriptorMap.Count + Id2DescriptorMap.Count);
                Id2DescriptorMap.Add(descriptor);
                SomeTypeStored = true;
            }

            public uint Descriptor2Id(ITypeDescriptor descriptor)
            {
                if (descriptor == null) return 0;
                int id;
                if (Descriptor2IdMap.TryGetValue(descriptor, out id))
                    return (uint) id;
                if (_typeSerializersMapping._descriptor2IdMap.TryGetValue(descriptor, out id))
                    return (uint) id;
                throw new InvalidOperationException();
            }

            public bool SomeTypeStored { get; private set; }
        }

        public void StoreObject(AbstractBufferedWriter writer, object obj)
        {
            if (obj == null)
            {
                writer.WriteUInt8(0);
                return;
            }
            int typeId;
            var descriptor = _typeSerializers.DescriptorOf(obj.GetType());
            if (_descriptor2IdMap.TryGetValue(descriptor, out typeId))
            {
                writer.WriteVUInt32((uint)typeId);
                _typeSerializers.GetSimpleSaver(descriptor)(writer, obj);
            }
        }
    }
}