// Low-level graphics helpers: merged-mesh builder, lighting shader, procedural textures.
using System.Numerics;
using Raylib_cs;
namespace SpaceBlocks;

public static class Col
{
    public static Color Hex(uint rgb, byte a = 255) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);
    public static Color Lerp(Color a, Color b, float t) => new((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)(a.A + (b.A - a.A) * t));
    public static Color Mul(Color c, float k) => new((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255), c.A);
}

// Collects non-indexed lit triangles. Vertex alpha = emissive amount (0 lit .. 255 fully self-lit).
public sealed unsafe class MeshBuilder
{
    readonly List<float> v = new(), n = new(), uv = new(); readonly List<byte> c = new();
    public Matrix4x4 T = Matrix4x4.Identity;
    public int Tris => v.Count / 9;

    Vector3 P(Vector3 p) => Vector3.Transform(p, T);
    Vector3 N(Vector3 p) => Vector3.Normalize(Vector3.TransformNormal(p, T));

    public void Tri(Vector3 a, Vector3 b, Vector3 cc, Color col, int emis = 0, Vector3? flatNormal = null)
    {
        var pa = P(a); var pb = P(b); var pc = P(cc);
        var nn = flatNormal.HasValue ? N(flatNormal.Value) : Vector3.Cross(pb - pa, pc - pa);
        if (!flatNormal.HasValue) { float l = nn.Length(); nn = l > 1e-9f ? nn / l : Vector3.UnitY; }
        Push(pa, nn, col, emis); Push(pb, nn, col, emis); Push(pc, nn, col, emis);
    }
    public void TriN(Vector3 a, Vector3 b, Vector3 cc, Vector3 na, Vector3 nb, Vector3 nc, Color col, int emis = 0)
    {
        Push(P(a), N(na), col, emis); Push(P(b), N(nb), col, emis); Push(P(cc), N(nc), col, emis);
    }
    void Push(Vector3 p, Vector3 nn, Color col, int emis, float u = 0, float w = 0)
    {
        uv.Add(u); uv.Add(w); v.Add(p.X); v.Add(p.Y); v.Add(p.Z); n.Add(nn.X); n.Add(nn.Y); n.Add(nn.Z);
        c.Add(col.R); c.Add(col.G); c.Add(col.B); c.Add((byte)emis);
    }
    // per-vertex normals, colours and texture coordinates (terrain, water, trails)
    public void TriFull(Vector3 a, Vector3 b, Vector3 cc, Vector3 na, Vector3 nb, Vector3 nc, Color ca, Color cb, Color cc2, Vector2 ua, Vector2 ub, Vector2 uc, int emis = 0)
    {
        Push(P(a), N(na), ca, emis, ua.X, ua.Y); Push(P(b), N(nb), cb, emis, ub.X, ub.Y); Push(P(cc), N(nc), cc2, emis, uc.X, uc.Y);
    }
    // copy another builder's geometry with a transform and a brightness tint (used to bake many trees into one mesh)
    public void Append(MeshBuilder o, Matrix4x4 xf, float tint = 1f)
    {
        Matrix4x4.Invert(xf, out var inv); var nm = Matrix4x4.Transpose(inv);
        for (int k = 0; k < o.v.Count; k += 3)
        {
            var p = Vector3.Transform(new Vector3(o.v[k], o.v[k + 1], o.v[k + 2]), xf); var nn = Vector3.Normalize(Vector3.TransformNormal(new Vector3(o.n[k], o.n[k + 1], o.n[k + 2]), nm)); int ci = k / 3 * 4, ui = k / 3 * 2;
            Push(P(p), N(nn), new Color((byte)Math.Min(255, o.c[ci] * tint), (byte)Math.Min(255, o.c[ci + 1] * tint), (byte)Math.Min(255, o.c[ci + 2] * tint), o.c[ci + 3]), o.c[ci + 3], o.uv[ui], o.uv[ui + 1]);
        }
    }
    public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col, int emis = 0) { Tri(a, b, cc, col, emis); Tri(a, cc, d, col, emis); }

    public void Box(float w, float h, float d, float x, float y, float z, Color col, int emis = 0)
    {
        float hx = w / 2, hy = h / 2, hz = d / 2; var o = new Vector3(x, y, z);
        Vector3 p(float a, float b, float cc) => o + new Vector3(a * hx, b * hy, cc * hz);
        Quad(p(-1, -1, 1), p(1, -1, 1), p(1, 1, 1), p(-1, 1, 1), col, emis);
        Quad(p(1, -1, -1), p(-1, -1, -1), p(-1, 1, -1), p(1, 1, -1), col, emis);
        Quad(p(-1, -1, -1), p(-1, -1, 1), p(-1, 1, 1), p(-1, 1, -1), col, emis);
        Quad(p(1, -1, 1), p(1, -1, -1), p(1, 1, -1), p(1, 1, 1), col, emis);
        Quad(p(-1, 1, 1), p(1, 1, 1), p(1, 1, -1), p(-1, 1, -1), Col.Mul(col, 1f), emis);
        Quad(p(-1, -1, -1), p(1, -1, -1), p(1, -1, 1), p(-1, -1, 1), col, emis);
    }
    // Cylinder / cone frustum along local Y, centered at (x,y,z), smooth side normals.
    public void CylY(float r1, float r2, float h, float x, float y, float z, Color col, int seg = 14, int emis = 0, bool caps = true)
    {
        float y0 = y - h / 2, y1 = y + h / 2; float slope = (r1 - r2) / MathF.Max(h, 1e-5f);
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathF.Tau / seg, a1 = (i + 1) * MathF.Tau / seg;
            Vector3 d0 = new(MathF.Cos(a0), 0, MathF.Sin(a0)), d1 = new(MathF.Cos(a1), 0, MathF.Sin(a1));
            Vector3 n0 = Vector3.Normalize(d0 + Vector3.UnitY * slope), n1 = Vector3.Normalize(d1 + Vector3.UnitY * slope);
            var b0 = new Vector3(x, y0, z) + d0 * r1; var b1 = new Vector3(x, y0, z) + d1 * r1;
            var t0 = new Vector3(x, y1, z) + d0 * r2; var t1 = new Vector3(x, y1, z) + d1 * r2;
            TriN(b0, b1, t1, n0, n1, n1, col, emis); TriN(b0, t1, t0, n0, n1, n0, col, emis);
            if (caps)
            {
                if (r2 > 1e-4f) Tri(new Vector3(x, y1, z), t0, t1, col, emis, Vector3.UnitY);
                if (r1 > 1e-4f) Tri(new Vector3(x, y0, z), b1, b0, col, emis, -Vector3.UnitY);
            }
        }
    }
    // Cylinder along local Z (nose pointing -Z like the web ships): r1 at +Z end, r2 at -Z end.
    public void CylZ(float r1, float r2, float len, float x, float y, float z, Color col, int seg = 12, int emis = 0)
    {
        var save = T; T = Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(x, y, z) * save;
        CylY(r1, r2, len, 0, 0, 0, col, seg, emis); T = save;
    }
    public void Sphere(float r, float x, float y, float z, Color col, float sx = 1, float sy = 1, float sz = 1, int emis = 0, int rings = 8, int seg = 12)
    {
        Vector3 q(int i, int j) { float th = MathF.PI * i / rings, ph = MathF.Tau * j / seg; return new Vector3(MathF.Sin(th) * MathF.Cos(ph), MathF.Cos(th), MathF.Sin(th) * MathF.Sin(ph)); }
        Vector3 pos(Vector3 u) => new(x + u.X * r * sx, y + u.Y * r * sy, z + u.Z * r * sz);
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < seg; j++)
            {
                Vector3 a = q(i, j), b = q(i + 1, j), cc = q(i + 1, j + 1), d = q(i, j + 1);
                if (i > 0) TriN(pos(a), pos(b), pos(d), a, b, d, col, emis);
                if (i < rings - 1) TriN(pos(b), pos(cc), pos(d), b, cc, d, col, emis);
            }
    }
    public void Octa(float r, float sy, Color col, int emis)
    {
        var top = new Vector3(0, r * sy, 0); var bot = new Vector3(0, -r * sy, 0);
        Vector3[] e = { new(r, 0, 0), new(0, 0, r), new(-r, 0, 0), new(0, 0, -r) };
        for (int i = 0; i < 4; i++) { Tri(top, e[(i + 1) % 4], e[i], col, emis); Tri(bot, e[i], e[(i + 1) % 4], col, emis); }
    }
    // Convex polygon in XZ extruded between y0 and y1.
    public void Extrude(Vector2[] poly, float y0, float y1, Color col, int emis = 0)
    {
        int k = poly.Length;
        for (int i = 1; i < k - 1; i++)
        {
            Tri(new Vector3(poly[0].X, y1, poly[0].Y), new Vector3(poly[i].X, y1, poly[i].Y), new Vector3(poly[i + 1].X, y1, poly[i + 1].Y), col, emis);
            Tri(new Vector3(poly[0].X, y0, poly[0].Y), new Vector3(poly[i + 1].X, y0, poly[i + 1].Y), new Vector3(poly[i].X, y0, poly[i].Y), col, emis);
        }
        for (int i = 0; i < k; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % k];
            Quad(new Vector3(a.X, y0, a.Y), new Vector3(b.X, y0, b.Y), new Vector3(b.X, y1, b.Y), new Vector3(a.X, y1, a.Y), col, emis);
        }
    }

    public Mesh Build()
    {
        var m = new Mesh { VertexCount = v.Count / 3, TriangleCount = v.Count / 9 };
        m.Vertices = (float*)Raylib.MemAlloc((uint)(v.Count * 4)); m.Normals = (float*)Raylib.MemAlloc((uint)(n.Count * 4));
        m.Colors = (byte*)Raylib.MemAlloc((uint)c.Count); m.TexCoords = (float*)Raylib.MemAlloc((uint)(v.Count / 3 * 2 * 4));
        for (int i = 0; i < v.Count; i++) { m.Vertices[i] = v[i]; m.Normals[i] = n[i]; }
        for (int i = 0; i < c.Count; i++) m.Colors[i] = c[i];
        for (int i = 0; i < uv.Count; i++) m.TexCoords[i] = uv[i];
        Raylib.UploadMesh(ref m, false);
        Raylib.MemFree(m.Vertices); m.Vertices = null; Raylib.MemFree(m.Normals); m.Normals = null; Raylib.MemFree(m.Colors); m.Colors = null; Raylib.MemFree(m.TexCoords); m.TexCoords = null; // the GPU has it now; drop the CPU copy
        return m;
    }
}

// Lit shader: hemisphere ambient + 2 directional + up to 8 point lights + exp fog; vertex alpha = emissive.
public sealed unsafe class LitShader
{
    public Shader Sh; public Material Mat;
    int locSun, locSunCol, locFill, locFillCol, locSky, locGnd, locFog, locFogCol, locNPt, locPtPos, locPtCol, locView, locGlow, locSpec, locPanel; public Texture2D PanelTex;
    const string VS = @"#version 330
layout(location=0) in vec3 vertexPosition; layout(location=2) in vec3 vertexNormal; layout(location=3) in vec4 vertexColor;
uniform mat4 mvp; uniform mat4 matModel; uniform mat4 matNormal;
out vec3 wpos; out vec3 wnorm; out vec4 vcol; out vec3 vLocal;
void main(){ vLocal=vertexPosition; vec4 w=matModel*vec4(vertexPosition,1.0); wpos=w.xyz; wnorm=normalize(mat3(matNormal)*vertexNormal); vcol=vertexColor; gl_Position=mvp*vec4(vertexPosition,1.0); }";
    const string FS = @"#version 330
in vec3 wpos; in vec3 wnorm; in vec4 vcol; in vec3 vLocal; out vec4 finalColor; uniform sampler2D texture0; uniform float uSpec; uniform float uPanel;
uniform vec3 uSun; uniform vec3 uSunCol; uniform vec3 uFill; uniform vec3 uFillCol; uniform vec3 uSky; uniform vec3 uGnd;
uniform vec2 uFog; uniform vec3 uFogCol; uniform float uGlow; uniform vec3 viewPos; uniform int uNPt; uniform vec3 uPtPos[8]; uniform vec4 uPtCol[8];
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14),0.0,1.0); }
void main(){
  vec3 n=normalize(wnorm); if(!gl_FrontFacing) n=-n;
  vec3 alb=pow(vcol.rgb,vec3(2.2)); float em=vcol.a;
  if(uPanel>0.0){ vec3 an=abs(n); an/=(an.x+an.y+an.z); vec3 pp=vLocal*uPanel; float tx=texture(texture0,pp.zy).r*an.x+texture(texture0,pp.xz).r*an.y+texture(texture0,pp.xy).r*an.z; alb*=mix(1.0,tx*1.15,0.8); vec3 cp=floor(vLocal*uPanel*4.0); float hsh=fract(sin(dot(cp,vec3(12.9898,78.233,37.719)))*43758.5453); alb*=0.84+0.3*hsh; }
  vec3 amb=mix(uGnd,uSky,n.y*0.5+0.5);
  vec3 l=amb+uSunCol*max(dot(n,normalize(uSun)),0.0)+uFillCol*max(dot(n,normalize(uFill)),0.0);
  for(int i=0;i<uNPt;i++){ vec3 d=uPtPos[i]-wpos; float dist=length(d); float r=uPtCol[i].a; float f=clamp(1.0-dist/r,0.0,1.0); l+=uPtCol[i].rgb*max(dot(n,d/max(dist,1e-3)),0.0)*f*f; }
  vec3 col=alb*l*(1.0-em)+alb*em*1.6*uGlow;
  vec3 V=normalize(viewPos-wpos); vec3 Hh=normalize(normalize(uSun)+V); float sp=pow(max(dot(n,Hh),0.0),56.0)*uSpec; col+=uSunCol*sp*(1.0-em);
  float rim=pow(1.0-max(dot(n,V),0.0),3.0); col+=uSky*rim*(0.10+0.22*uSpec)*(1.0-em);
  float dist=length(viewPos-wpos); float fd=uFog.x*dist; float fog=1.0-exp(-fd*fd);
  col=mix(col,uFogCol,clamp(fog,0.0,1.0));
  finalColor=vec4(pow(aces(col),vec3(1.0/2.2)),1.0);
}";
    public LitShader()
    {
        Sh = Raylib.LoadShaderFromMemory(VS, FS);
        Sh.Locs[(int)ShaderLocationIndex.MatrixModel] = Raylib.GetShaderLocation(Sh, "matModel");
        Sh.Locs[(int)ShaderLocationIndex.MatrixNormal] = Raylib.GetShaderLocation(Sh, "matNormal");
        Sh.Locs[(int)ShaderLocationIndex.VectorView] = Raylib.GetShaderLocation(Sh, "viewPos");
        locView = Sh.Locs[(int)ShaderLocationIndex.VectorView];
        locSun = L("uSun"); locSunCol = L("uSunCol"); locFill = L("uFill"); locFillCol = L("uFillCol"); locSky = L("uSky"); locGnd = L("uGnd");
        locFog = L("uFog"); locFogCol = L("uFogCol"); locNPt = L("uNPt"); locPtPos = L("uPtPos"); locPtCol = L("uPtCol"); locGlow = L("uGlow"); locSpec = L("uSpec"); locPanel = L("uPanel");
        Mat = Raylib.LoadMaterialDefault(); Mat.Shader = Sh; PanelTex = Tex.Panel(); Mat.Maps[(int)MaterialMapIndex.Albedo].Texture = PanelTex;
    }
    int L(string s) => Raylib.GetShaderLocation(Sh, s);
    void V3(int loc, Vector3 a) { float* p = stackalloc float[3] { a.X, a.Y, a.Z }; Raylib.SetShaderValue(Sh, loc, p, ShaderUniformDataType.Vec3); }

    public void SetExterior(Vector3 camPos)
    {
        V3(locView, camPos);
        V3(locSun, new(-.5f, .75f, .38f)); V3(locSunCol, new(1.25f, 1.05f, .82f));
        V3(locFill, new(.6f, -.15f, -.3f)); V3(locFillCol, new(.18f, .22f, .5f));
        V3(locSky, new Vector3(.42f, .5f, .85f) * .75f); V3(locGnd, new Vector3(.18f, .1f, .3f) * .7f);
        float* f = stackalloc float[2] { .00007f * 1.0f, 0 }; Raylib.SetShaderValue(Sh, locFog, f, ShaderUniformDataType.Vec2);
        V3(locFogCol, new(.01f, .02f, .06f)); int z = 0; Raylib.SetShaderValue(Sh, locNPt, &z, ShaderUniformDataType.Int);
    }
    public void SetInterior(Vector3 camPos, Vector3[] pos, float[] rad, Vector3[] col)
    {
        V3(locView, camPos);
        V3(locSun, new(-.4f, .8f, -.5f)); V3(locSunCol, new(.25f, .22f, .2f));
        V3(locFill, new(.4f, .2f, .6f)); V3(locFillCol, new(.08f, .09f, .14f));
        V3(locSky, new Vector3(.34f, .4f, .6f) * .45f); V3(locGnd, new Vector3(.14f, .12f, .2f) * .5f);
        float* f = stackalloc float[2] { .0005f, 0 }; Raylib.SetShaderValue(Sh, locFog, f, ShaderUniformDataType.Vec2);
        V3(locFogCol, new(.02f, .03f, .06f));
        int n = Math.Min(8, pos.Length); Raylib.SetShaderValue(Sh, locNPt, &n, ShaderUniformDataType.Int);
        float* pp = stackalloc float[8 * 3]; float* pc = stackalloc float[8 * 4];
        for (int i = 0; i < n; i++) { pp[i * 3] = pos[i].X; pp[i * 3 + 1] = pos[i].Y; pp[i * 3 + 2] = pos[i].Z; pc[i * 4] = col[i].X; pc[i * 4 + 1] = col[i].Y; pc[i * 4 + 2] = col[i].Z; pc[i * 4 + 3] = rad[i]; }
        Raylib.SetShaderValueV(Sh, locPtPos, pp, ShaderUniformDataType.Vec3, n);
        Raylib.SetShaderValueV(Sh, locPtCol, pc, ShaderUniformDataType.Vec4, n);
    }
    public void Draw(Mesh mesh, Matrix4x4 xf, float glow = 1f, float spec = 0f, float panel = 0f) { float g = glow, sp = spec, pn = panel; Raylib.SetShaderValue(Sh, locGlow, &g, ShaderUniformDataType.Float); Raylib.SetShaderValue(Sh, locSpec, &sp, ShaderUniformDataType.Float); Raylib.SetShaderValue(Sh, locPanel, &pn, ShaderUniformDataType.Float); Raylib.DrawMesh(mesh, Mat, Matrix4x4.Transpose(xf)); }
}

public static class Tex
{
    public static Texture2D Glow(int size = 64)
    {
        var img = Raylib.GenImageColor(size, size, new Color(0, 0, 0, 0));
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float d = MathF.Sqrt((x - size / 2f + .5f) * (x - size / 2f + .5f) + (y - size / 2f + .5f) * (y - size / 2f + .5f)) / (size / 2f);
            float a = d >= 1 ? 0 : d < .25f ? 1 - d / .25f * .45f : .55f * (1 - (d - .25f) / .75f);
            Raylib.ImageDrawPixel(ref img, x, y, new Color((byte)255, (byte)255, (byte)255, (byte)(Math.Clamp(a, 0, 1) * 255)));
        }
        var t = Raylib.LoadTextureFromImage(img); Raylib.UnloadImage(img); Raylib.SetTextureFilter(t, TextureFilter.Bilinear); return t;
    }
        // tileable brushed-metal panel plating: seams, rivets, scratches and grime (sampled triplanar in the lit shader)
    public static Texture2D Panel(int size = 256)
    {
        var rng = new Random(77); var img = Raylib.GenImageColor(size, size, new Color((byte)200, (byte)200, (byte)200, (byte)255)); int cell = size / 4;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float v = 205 + (float)(rng.NextDouble() - .5) * 18 + MathF.Sin(x * .9f + (y / cell) * 3f) * 3f; int gx = x % cell, gy = y % cell;
            if (gx < 2 || gy < 2) v -= 58; else if (gx == 2 || gy == 2) v += 14; // dark seam with a lit lip
            if ((gx == 7 || gx == 8 || gx == cell - 8 || gx == cell - 9) && (gy == 7 || gy == 8 || gy == cell - 8 || gy == cell - 9)) v += 34; // rivets
            Raylib.ImageDrawPixel(ref img, x, y, new Color((byte)Math.Clamp(v, 0, 255), (byte)Math.Clamp(v, 0, 255), (byte)Math.Clamp(v, 0, 255), (byte)255));
        }
        for (int k = 0; k < 40; k++) { int x0 = rng.Next(size), y0 = rng.Next(size), l = 6 + rng.Next(22); for (int j = 0; j < l; j++) Raylib.ImageDrawPixel(ref img, (x0 + j) % size, (y0 + j / 4) % size, new Color((byte)165, (byte)165, (byte)165, (byte)255)); }
        var t = Raylib.LoadTextureFromImage(img); Raylib.UnloadImage(img); Raylib.GenTextureMipmaps(ref t); Raylib.SetTextureFilter(t, TextureFilter.Trilinear); Raylib.SetTextureWrap(t, TextureWrap.Repeat); return t;
    }
    public static Texture2D Reticle(int size = 128)
    {
        var img = Raylib.GenImageColor(size, size, new Color(0, 0, 0, 0));
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = x - size / 2f + .5f, dy = y - size / 2f + .5f, d = MathF.Sqrt(dx * dx + dy * dy) / (size / 2f), ang = MathF.Atan2(dy, dx);
            float seg = ((ang + MathF.PI) % (MathF.PI / 2)) / (MathF.PI / 2);
            if (MathF.Abs(d - .85f) < .022f && seg > .1f && seg < .72f) Raylib.ImageDrawPixel(ref img, x, y, new Color(90, 220, 255, 255));
        }
        var t = Raylib.LoadTextureFromImage(img); Raylib.UnloadImage(img); Raylib.SetTextureFilter(t, TextureFilter.Bilinear); return t;
    }
    public static Texture2D Waterfall(int w = 64, int h = 256)
    {
        var rng = new Random(11); var img = Raylib.GenImageColor(w, h, new Color(0, 0, 0, 0));
        for (int k = 0; k < 26; k++)
        {
            int xx = rng.Next(w - 4), l = 30 + rng.Next(90), y0 = rng.Next(h), ww = 2 + rng.Next(3);
            for (int j = 0; j < l; j++)
            {
                float f = j / (float)l, a = f < .5f ? f * 2 : (1 - f) * 2; int yy = (y0 + j) % h;
                for (int i = 0; i < ww; i++) Raylib.ImageDrawPixel(ref img, xx + i, yy, new Color((byte)220, (byte)240, (byte)255, (byte)(a * 220)));
            }
        }
        var t = Raylib.LoadTextureFromImage(img); Raylib.UnloadImage(img); Raylib.SetTextureWrap(t, TextureWrap.Repeat); Raylib.SetTextureFilter(t, TextureFilter.Bilinear); return t;
    }
    // Equirect-ish star + nebula map for the sky sphere.
    public static Texture2D Sky(int w = 4096, int h = 2048)
    {
        var rng = new Random(3); var img = Raylib.GenImageColor(w, h, new Color(7, 10, 22, 255));
        (int r, int g, int b)[] nc = { (120, 70, 255), (40, 170, 200), (255, 90, 150), (70, 90, 255) };
        for (int i = 0; i < 4; i++)
        {
            int cx = (int)(((i * 1.7f + .4f) % 6.283f) / 6.283f * w), cy = (int)(h * (.5f + (i % 2 == 0 ? -.12f : .1f))), R = 300 + i * 36;
            for (int y = Math.Max(0, cy - R); y < Math.Min(h, cy + R); y++) for (int x = cx - R; x < cx + R; x++)
            {
                float d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / R; if (d >= 1) continue;
                float a = (1 - d) * (1 - d) * .32f; int xx = (x % w + w) % w; var p = Raylib.GetImageColor(img, xx, y);
                Raylib.ImageDrawPixel(ref img, xx, y, new Color((byte)Math.Min(255f, p.R + nc[i].r * a), (byte)Math.Min(255f, p.G + nc[i].g * a), (byte)Math.Min(255f, p.B + nc[i].b * a), (byte)255));
            }
        }
        for (int k = 0; k < 9000; k++)
        {
            int x = rng.Next(w), y = rng.Next(h); float kk = .45f + (float)rng.NextDouble() * .55f; double tint = rng.NextDouble();
            var c = new Color((byte)(255 * kk * (tint < .2 ? 1 : .85)), (byte)(255 * kk * .9), (byte)(255 * kk * (tint > .7 ? 1 : .9)), (byte)255);
            Raylib.ImageDrawPixel(ref img, x, y, c); if (kk > .8f) { Raylib.ImageDrawPixel(ref img, (x + 1) % w, y, c); Raylib.ImageDrawPixel(ref img, x, Math.Min(h - 1, y + 1), c); Raylib.ImageDrawPixel(ref img, (x + 1) % w, Math.Min(h - 1, y + 1), c); }
        }
        var t = Raylib.LoadTextureFromImage(img); Raylib.UnloadImage(img); Raylib.SetTextureFilter(t, TextureFilter.Bilinear); return t;
    }
}

// Animated water: flowing streaks, edge foam, sun glints; vertex colour = tint, alpha = how much of a waterfall it is (0 river .. 1 fall).
public sealed unsafe class WaterShader
{
    public Shader Sh; public Material Mat; readonly int locTime, locView, locSun;
    const string VS = @"#version 330
layout(location=0) in vec3 vertexPosition; layout(location=1) in vec2 vertexTexCoord; layout(location=3) in vec4 vertexColor;
uniform mat4 mvp; uniform mat4 matModel; out vec2 uv; out vec4 col; out vec3 wpos;
void main(){ uv=vertexTexCoord; col=vertexColor; wpos=(matModel*vec4(vertexPosition,1.0)).xyz; gl_Position=mvp*vec4(vertexPosition,1.0); }";
    const string FS = @"#version 330
in vec2 uv; in vec4 col; in vec3 wpos; out vec4 finalColor; uniform float uTime; uniform vec3 viewPos; uniform vec3 uSun;
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14),0.0,1.0); }
void main(){
  float fall=col.a; float speed=mix(0.45,2.2,fall);
  vec2 p=vec2(uv.x*3.2, uv.y*2.2-uTime*speed);
  float w1=sin(p.y*7.0+sin(p.x*5.0+uTime*0.8)*1.6)*0.5+0.5, w2=sin(p.y*13.0-p.x*8.0+uTime*speed*1.7)*0.5+0.5, w3=sin(p.y*21.0+p.x*4.0-uTime*speed*2.3)*0.5+0.5;
  float streak=smoothstep(0.60,0.95,w1*0.5+w2*0.35+w3*0.25);
  vec3 deep=col.rgb*0.55, shallow=col.rgb*1.25;
  vec3 base=mix(deep,shallow,w1*0.55+0.2);
  base=mix(base,vec3(0.92,0.98,1.0),streak*(0.30+0.55*fall));
  float edge=smoothstep(0.0,0.16,uv.x)*smoothstep(1.0,0.84,uv.x);
  base=mix(vec3(0.88,0.96,1.0),base,mix(0.55,1.0,edge));
  vec3 n=normalize(vec3((w2-0.5)*0.12,1.0,(w3-0.5)*0.12)); vec3 L=normalize(uSun); vec3 V=normalize(viewPos-wpos);
  float spec=pow(max(dot(reflect(-L,n),V),0.0),70.0); float fres=pow(1.0-max(dot(n,V),0.0),3.0);
  vec3 c=base*(0.55+0.45*max(dot(n,L),0.0)+0.25)+vec3(1.0,0.97,0.9)*spec*0.9+vec3(0.5,0.7,1.0)*fres*0.25*(1.0-fall);
  float d=length(viewPos-wpos); float fg=1.0-exp(-pow(0.00007*d,2.0)); c=mix(c,vec3(0.01,0.02,0.06),clamp(fg,0.0,1.0));
  float a=mix(0.84,0.72,fall)*(0.55+0.45*edge); finalColor=vec4(pow(aces(c),vec3(1.0/2.2)),a);
}";
    public WaterShader()
    {
        Sh = Raylib.LoadShaderFromMemory(VS, FS); Sh.Locs[(int)ShaderLocationIndex.MatrixModel] = Raylib.GetShaderLocation(Sh, "matModel");
        locTime = Raylib.GetShaderLocation(Sh, "uTime"); locView = Raylib.GetShaderLocation(Sh, "viewPos"); locSun = Raylib.GetShaderLocation(Sh, "uSun");
        Mat = Raylib.LoadMaterialDefault(); Mat.Shader = Sh;
    }
    public void Begin(float t, Vector3 cam)
    {
        float tt = t; Raylib.SetShaderValue(Sh, locTime, &tt, ShaderUniformDataType.Float);
        float* p = stackalloc float[3] { cam.X, cam.Y, cam.Z }; Raylib.SetShaderValue(Sh, locView, p, ShaderUniformDataType.Vec3);
        float* s = stackalloc float[3] { -.5f, .75f, .38f }; Raylib.SetShaderValue(Sh, locSun, s, ShaderUniformDataType.Vec3);
    }
    public void Draw(Mesh mesh, Matrix4x4 xf) => Raylib.DrawMesh(mesh, Mat, Matrix4x4.Transpose(xf));
}

