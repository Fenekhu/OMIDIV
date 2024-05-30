using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class GeometryUtil {

    static GeometryUtil() {
        // pre-generate up to 8-sides shapes
        for (uint i = 2; i < 8; i++) {
            GetNSidedPrismMesh(i);
            GetNSidedPlaneMesh(i);
        }
    }

    private static readonly Dictionary<uint, Mesh> NgonPrisms = new Dictionary<uint, Mesh>();
    private static readonly Dictionary<uint, Mesh> NgonPlanes = new Dictionary<uint, Mesh>();

    public static Mesh GetNSidedPrismMesh(uint sides) {
        if (sides < 2) return null;
        if (NgonPrisms.TryGetValue(sides, out Mesh result)) return result;

        Mesh ret = new Mesh();
        Vector3[] vertices = new Vector3[sides * 6];
        Vector3[] normals = new Vector3[sides * 6];
        int[] indicies = new int[(sides-1) * 12]; // (sides * 6) + (sides - 2)*3*2 = (sides-1) * 12

        const float hpi = Mathf.PI / 2; // to rotate notes so 0 degrees is down
        const float scl = 0.70710678118654752440084436210485f; // 0.5 * root2. root2 is to make squares full size, future of this variable tbd.

        for (int i = 0; i < sides; i++) {
            // the actual four verticies of each face
            Vector3 a = new Vector3( 0.5f, 0);
            Vector3 b = new Vector3( 0.5f, 0);
            Vector3 c = new Vector3(-0.5f, 0);
            Vector3 d = new Vector3(-0.5f, 0);

            // this part does 2D math relative to a unit circle
            float arc = Mathf.PI * 2 / sides; // angle between sides. eg 90 for square, 120 for triangle
            // angle for one vertex, the center of the side, and the other vertex, all rotated relative to down = 0 deg instead of right = 0 deg
            float angle0 = arc * (i-0.5f) - hpi;
            float angle1 = arc * i - hpi;
            float angle2 = arc * (i+0.5f) - hpi;
            // both vertexes and the outward normal vector of each face between
            Vector2 p0 = new Vector2(Mathf.Sin(angle0) * scl, Mathf.Cos(angle0) * scl);
            Vector3 norm = new Vector3(0, Mathf.Sin(angle1), Mathf.Cos(angle1));
            Vector2 p1 = new Vector2(Mathf.Sin(angle2) * scl, Mathf.Cos(angle2) * scl);

            // now we apply our 2D math to generate the 3D faces.
            (a.y, a.z) = (p0.x, p0.y);
            (b.y, b.z) = (p1.x, p1.y);
            (c.y, c.z) = (p0.x, p0.y);
            (d.y, d.z) = (p1.x, p1.y);

            // finally, write these verticies to the mesh data arrays
            vertices[i*6+0] = a;
            vertices[i*6+1] = b;
            vertices[i*6+2] = c;
            vertices[i*6+3] = d;
            vertices[i*6+4] = a;
            vertices[i*6+5] = c;

            // the normals at each vertex are the normals to the entire plane that a,b,c,d form
            for (int j = 0; j < 4; j++) normals[i*6+j] = norm;
            // these are the normals for the end caps.
            normals[i*6+4] = Vector3.right;
            normals[i*6+5] = Vector3.left;

            indicies[i*6+0] = i*6+0;
            indicies[i*6+1] = i*6+1;
            indicies[i*6+2] = i*6+2;
            indicies[i*6+3] = i*6+1;
            indicies[i*6+4] = i*6+3;
            indicies[i*6+5] = i*6+2;
        }

        for (int i = 0; i < sides - 2; i++) {
            indicies[sides*6 + i*6 + 2] = 4; // the winding of these has to be reversed
            indicies[sides*6 + i*6 + 1] = i*6+10; // (i+1)*6+4
            indicies[sides*6 + i*6 + 0] = i*6+16; // (i+2)*6+4
            indicies[sides*6 + i*6 + 3] = 5;
            indicies[sides*6 + i*6 + 4] = i*6+11; // (i+1)*6+5
            indicies[sides*6 + i*6 + 5] = i*6+17; // (i+2)*6+5
        }

        ret.vertices = vertices;
        ret.normals = normals;
        ret.triangles = indicies;

        NgonPrisms[sides] = ret;
        return ret;
    }

    public static Mesh GetNSidedPlaneMesh(uint sides) {
        if (sides < 3) return null;
        if (NgonPlanes.TryGetValue(sides, out Mesh result)) return result;

        const float hpi = Mathf.PI / 2; // to rotate notes so 0 degrees is down
        const float scl = 0.70710678118654752440084436210485f; // 0.5 * root2. root2 is to make squares full size, future of this variable tbd.

        Mesh ret = new Mesh();

        Vector3[] vertices = new Vector3[sides];
        Vector3[] normals = new Vector3[sides];
        int[] indicies = new int[(sides-2) * 3];

        for (int i = 0; i < sides; i++) {
            float arc = Mathf.PI * 2 / sides;
            float angle0 = arc * (i-0.5f) - hpi;

            vertices[i] = new Vector3(Mathf.Sin(angle0) * scl, Mathf.Cos(angle0) * scl);
            normals[i] = Vector3.back;
        }

        for (int i = 0; i < sides - 2; i++) {
            indicies[i*3+0] = i;
            indicies[i*3+1] = i+1;
            indicies[i*3+2] = i+2;
        }

        ret.vertices = vertices;
        ret.normals = normals;
        ret.triangles = indicies;

        NgonPlanes[sides] = ret;
        return ret;
    }

}
