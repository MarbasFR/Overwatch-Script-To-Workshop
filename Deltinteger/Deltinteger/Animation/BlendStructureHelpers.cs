using System.Linq;
using System.Collections.Generic;
using Deltin.Deltinteger.Elements;

namespace Deltin.Deltinteger.Animation
{
    public static class BlendStructureHelper
    {
        /// <summary>Converts an array of BlendVertex-es into an Element array where every i element is a vertex of the local location and every i + 1 element is an array of groups.</summary>
        public static Element VerticesToElementWithGroups(BlendVertex[] vertices)
        {
            var array = new Element[vertices.Length * 2];
            for (int i = 0; i < array.Length; i++)
            {
                array[i * 2] = vertices[i].Vertex.ToVector();
                
                // Get the groups.
                var groupArray = new Element[vertices[i].Groups.Length * 2];
                for (int g = 0; g < groupArray.Length; g++)
                {
                    groupArray[g * 2] = vertices[i].Groups[g].Group;
                    groupArray[(g * 2) + 1] = vertices[i].Groups[g].Weight;
                }

                // Add the group array.
                array[(i * 2) + 1] = Element.CreateArray(groupArray);
            }

            return Element.CreateArray(array);
        }

        /// <summary>Converts an array of BlendVertex-es to a Element vector array.</summary>
        public static Element VerticesToElement(BlendVertex[] vertices)
        {
            var array = new Element[vertices.Length];
            for (int i = 0; i < array.Length; i++)
                array[i] = vertices[i].Vertex.ToVector();
            return Element.CreateArray(array);
        }
    }

    class BoneLinker
    {
        public List<Element> PointData { get; } = new List<Element>();
        public List<BoneData> BoneData { get; } = new List<BoneData>();
        private readonly BlendFile _file;
        private readonly BlendArmature _armature;

        public BoneLinker(BlendFile file, BlendArmature armature)
        {
            _file = file;
            _armature = armature;

            // We link the bones by choosing the bones without a parent then recursively
            // linking the children.
            // A bone without a parent will have their Parent value set to -1.
            var rootBones = armature.Bones.Where(bone => bone.Parent == -1).ToArray();
            
            foreach (var bone in rootBones)
            {
                // Create a point for the head and tail of the bone.
                PointData.Add(bone.HeadLocal.ToVector());
                PointData.Add(bone.TailLocal.ToVector());

                // Create the bone link data then add it to the BoneData list.
                BoneData root = new BoneData(bone, PointData.Count - 2, PointData.Count - 1);
                BoneData.Add(root);

                // Link the children recursively.
                GetChildBoneData(root, bone);
            }
        }

        void GetChildBoneData(BoneData data, Bone bone)
        {
            // Iterate through each child.
            foreach(var childIndex in bone.Children)
            {
                var child = _armature.Bones[childIndex]; // Get the actual child from the index.
                var childData = new BoneData(child); // Create the bone link data.
                BoneData.Add(childData); // Add the newly-created bone line to the BoneData list.

                // If the child is connected, reuse the point at the parent's tail (data.Tail).
                if (child.IsConnected)
                    childData.Head = data.Tail;
                else // Otherwise, create a new point.
                {
                    PointData.Add(child.Head.ToVector());
                    childData.Head = PointData.Count - 1;
                }

                // Create the tail point.
                PointData.Add(child.Tail.ToVector());
                childData.Tail = PointData.Count - 1;

                // Recursively get this bone's children.
                GetChildBoneData(childData, child);
            }
        }
    }

    class BoneData
    {
        public Bone Original { get; }
        public int Head { get; set; }
        public int Tail { get; set; }

        public BoneData(Bone original, int head, int tail)
        {
            Original = original;
            Head = head;
            Tail = tail;
        }

        public BoneData(Bone original)
        {
            Original = original;
        }
    }
}