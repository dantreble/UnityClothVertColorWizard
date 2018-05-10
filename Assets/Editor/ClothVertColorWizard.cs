using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ClothVertColorWizard : ScriptableWizard
{
    public SkinnedMeshRenderer m_source;
    public Cloth m_dest;

    public enum Channel
    {
        Red,
        Green,
        Blue,
        Alpha,
    };

    public Channel m_distanceChannel = Channel.Red;
    public bool m_distanceChannelInvert = false;
    public float m_distanceMultiplier = 1.0f; 

    public float m_cellSize = 0.001f;

    [MenuItem("Custom/Cloth/Vert Colour")]
    static void CreateWizard()
    {
        var clothVertColorWizard = DisplayWizard("Copy Vert Colours to Constraints", typeof(ClothVertColorWizard), "Copy") as ClothVertColorWizard;

        var activeGameObject = Selection.activeGameObject;

        clothVertColorWizard.m_source = activeGameObject.GetComponent<SkinnedMeshRenderer>();
        clothVertColorWizard.m_dest = activeGameObject.GetComponent<Cloth>();
    }

    struct Vertex3Hashed
    {
        private readonly Vector3 vector;
        private readonly Color color;
        private readonly int cellX;
        private readonly int cellY;
        private readonly int cellZ;

        public Vertex3Hashed(Vector3 a_source, Color a_color, float a_cellSize)
        {
            vector = a_source;
            color = a_color;

            cellX = (int) (vector.x/a_cellSize);
            cellY = (int) (vector.y/a_cellSize);
            cellZ = (int) (vector.z/a_cellSize);
        }

        public Vector3 Vector
        {
            get { return vector; }
        }

        public Color Color
        {
            get { return color; }
        }

        public override int GetHashCode()
        {
            return Hash(cellX, cellY, cellZ);            
        }

        public List<int> GetSurroundingHashCodes(int a_level)
        {
            var singleDimension = (1 + (a_level*2));

            var totalCells = singleDimension*singleDimension*singleDimension;

            var hashes = new List<int>(totalCells);

            for (var x = cellX - a_level; x <= cellX + a_level; ++x)
            {
                for (var y = cellY - a_level; y <= cellY + a_level; ++y)
                {
                    for (var z = cellZ - a_level; z <= cellZ + a_level; ++z)
                    {
                        hashes.Add(Hash(x, y, z));
                    }
                }
            }

            return hashes;
        }

        private static int Hash(int a_x, int a_y, int a_z)
        {
            const int magic1 = 2147483647; // Large multiplicative constants;
            const int magic2 = 2147483629; // here arbitrarily chosen primes
            const int magic3 = 2147483587; // 

            var hash = (a_x*magic1) + (a_y*magic2) + (a_z*magic3);
            return hash;
        }
    }



    public void OnWizardCreate()
    {
        if (m_source == null)
        {
            Debug.LogError("Source is null");
            return;
        }

        if (m_dest == null)
        {
            Debug.LogError("Dest is null");
            return;
        }

        var vertColours = m_source.sharedMesh.colors;

        if (vertColours.Length == 0)
        {
            Debug.LogError("Mesh: " + m_source.sharedMesh.name + " has no vert colours");
            return;
        }

        var bakedSkin = new Mesh();
        m_source.BakeMesh(bakedSkin);

        var coefficients = new ClothSkinningCoefficient[m_dest.coefficients.Length];

        var vertexLookup = new Dictionary<int, List<Vertex3Hashed>>();

        var skinTransformNoScale = Matrix4x4.TRS(m_source.transform.position, m_source.transform.rotation, Vector3.one);

        for (int index = 0; index < bakedSkin.vertices.Length; index++)
        {
            var skinVertWorldSpace = skinTransformNoScale.MultiplyPoint3x4(bakedSkin.vertices[index]); //m_source.transform.TransformPoint(bakedSkin.vertices[index]);

            var vertex3Hashed = new Vertex3Hashed(skinVertWorldSpace, bakedSkin.colors[index], m_cellSize);

            List<Vertex3Hashed> cellList;
            if (!vertexLookup.TryGetValue(vertex3Hashed.GetHashCode(), out cellList))
            {
                vertexLookup.Add(vertex3Hashed.GetHashCode(), new List<Vertex3Hashed>(1) { vertex3Hashed });
            }
            else
            {
                cellList.Add(vertex3Hashed);
            }
        }

        var destSkin = m_dest.GetComponent<SkinnedMeshRenderer>();
        var rootBone = destSkin.rootBone != null ? destSkin.rootBone : destSkin.transform;
        var rootBoneTransformNoScale = Matrix4x4.TRS(rootBone.position, rootBone.rotation, Vector3.one);

        for (var i = 0; i < m_dest.coefficients.Length; ++i)
        {
            //Find nearest vert in source mesh
            var closestVertDistance = float.MaxValue;
            var closestVertColour = Color.black;
            
            var clothVertWorldSpace = rootBoneTransformNoScale.MultiplyPoint3x4(m_dest.vertices[i]);

            //Check the cube of 27 cells around me
            var hashList = new Vertex3Hashed(clothVertWorldSpace, Color.black, m_cellSize).GetSurroundingHashCodes(1);

            foreach (var hash in hashList)
            {
                List<Vertex3Hashed> cellList;
                if (vertexLookup.TryGetValue(hash, out cellList))
                {
                    foreach (var vertex3Hashed in cellList)
                    {
                        var dist = Vector3.Distance(clothVertWorldSpace, vertex3Hashed.Vector);

                        if (dist < closestVertDistance)
                        {
                            closestVertColour = vertex3Hashed.Color;
                            closestVertDistance = dist;
                        }
                    }
                }
            }
         
            float maxDistance;
            switch(m_distanceChannel)
            {
                default:
                case Channel.Red:
                    maxDistance = closestVertColour.r;
                    break;
                case Channel.Green:
                    maxDistance = closestVertColour.g;
                    break;
                case Channel.Blue:
                    maxDistance = closestVertColour.b;
                    break;
                case Channel.Alpha:
                    maxDistance = closestVertColour.a;
                    break;
            }

            if(m_distanceChannelInvert)
            {
                maxDistance = 1f - maxDistance;
            }

            maxDistance = m_distanceMultiplier * maxDistance;

            coefficients[i].maxDistance = maxDistance;
            coefficients[i].collisionSphereDistance = float.MaxValue;
        }

        m_dest.coefficients = coefficients;

    }

	

}