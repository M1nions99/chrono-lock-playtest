using UnityEngine;
using UnityEngine.UI;

namespace ChronoLock
{
    // Original vector artwork, rendered at the current UI resolution.
    public sealed class ChronoBackdrop : MaskableGraphic
    {
        float phase;
        void Update()
        {
            if(!ChronoPreferences.Current.cameraMotion)return;
            phase=Time.unscaledTime*.055f;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r=rectTransform.rect;
            var left=new Color(.015f,.034f,.052f);
            var right=new Color(.035f,.092f,.118f);
            Gradient(vh,r,left,right);
            Vector2 c=new Vector2(r.xMin+r.width*.734f,r.yMin+r.height*.68f);
            float radius=Mathf.Min(r.height*.214f,r.width*.15f);
            for(int i=0;i<16;i++)
            {
                float x=r.xMin+r.width*(.50f+i*.033f);
                Quad(vh,new Rect(x,r.yMin,1,r.height),new Color(.22f,.48f,.53f,.05f));
            }
            for(int i=0;i<14;i++)
                Quad(vh,new Rect(r.xMin+r.width*.50f,r.yMin+r.height*i/14,r.width*.50f,1),new Color(.22f,.48f,.53f,.05f));
            for(int i=14;i>=1;i--)
                Disk(vh,c,radius*(1.6f+i*.018f),new Color(.07f,.50f,.52f,.009f));
            Disk(vh,c,radius*.69f,new Color(.035f,.105f,.13f,.95f));
            Ring(vh,c,radius*1.18f,1,0,Mathf.PI*2,new Color(.23f,.57f,.59f,.26f));
            Ring(vh,c,radius,2,-phase,Mathf.PI*1.38f-phase,new Color(.34f,.9f,.86f,.8f));
            Ring(vh,c,radius*.96f,1,-phase,Mathf.PI*1.38f-phase,new Color(.22f,.48f,.52f,.26f));
            Ring(vh,c,radius*.81f,11,phase+1.9f,phase+3.36f,new Color(.30f,.68f,.70f,.45f));
            Ring(vh,c,radius*.81f,2,phase+3.36f,phase+7.9f,new Color(.45f,.67f,.72f,.30f));
            Ring(vh,c,radius*.64f,1,0,Mathf.PI*2,new Color(.36f,.61f,.65f,.4f));
            for(int i=0;i<72;i++)
            {
                float a=i*Mathf.PI*2/72;
                Vector2 p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*1.1f;
                Vector2 q=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*(i%6==0?1.15f:1.12f);
                Segment(vh,p,q,1,new Color(.38f,.61f,.64f,i%6==0?.65f:.32f));
            }
            // Three distinct temporal paths.
            for(int i=0;i<3;i++)
            {
                float a=phase*.6f+i*Mathf.PI*2/3;
                Vector2 p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*.44f;
                Vector2 q=c+new Vector2(Mathf.Cos(a+1.5f),Mathf.Sin(a+1.5f))*radius*.44f;
                Segment(vh,p,q,2,new Color(.49f,.85f,.83f,.7f));
                Disk(vh,p,5,new Color(.63f,.99f,.93f));
            }
            Disk(vh,c,4,new Color(.62f,.91f,.9f));
            Ring(vh,c,20,1,0,Mathf.PI*2,new Color(.49f,.81f,.82f,.35f));
            Quad(vh,new Rect(r.xMin+54,r.yMax-49,38,3),new Color(.34f,.91f,.88f));
            Quad(vh,new Rect(r.xMin+54,r.yMin+49,r.width-108,1),new Color(.17f,.31f,.35f,.45f));
        }
        static void Gradient(VertexHelper vh,Rect r,Color a,Color b)
        {
            int n=vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin,r.yMin),a,Vector2.zero);vh.AddVert(new Vector3(r.xMin,r.yMax),a,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMax),b,Vector2.zero);vh.AddVert(new Vector3(r.xMax,r.yMin),b,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void Quad(VertexHelper vh,Rect r,Color c){Gradient(vh,r,c,c);}
        static void Segment(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 p=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int n=vh.currentVertCount;
            vh.AddVert(a+p,color,Vector2.zero);vh.AddVert(a-p,color,Vector2.zero);vh.AddVert(b-p,color,Vector2.zero);vh.AddVert(b+p,color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        static void Ring(VertexHelper vh,Vector2 c,float radius,float width,float from,float to,Color color)
        {
            int steps=Mathf.CeilToInt((to-from)*radius/7);
            for(int i=0;i<steps;i++){float a=Mathf.Lerp(from,to,i/(float)steps),b=Mathf.Lerp(from,to,(i+1f)/steps);Segment(vh,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color);}
        }
        static void Disk(VertexHelper vh,Vector2 c,float radius,Color color)
        {
            int n=vh.currentVertCount;vh.AddVert(c,color,Vector2.zero);
            const int segments=72;
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;vh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);if(i>0)vh.AddTriangle(n,n+i,n+i+1);}
        }
    }
}

