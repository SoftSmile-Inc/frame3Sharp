﻿using UnityEngine;
using System;
using g3;

namespace f3
{
	public class BoxSO : PrimitiveSO, SpatialQueryableSO
    {
		float width;  // x
		float height; // y
		float depth;  // z

		GameObject box;
		GameObject boxMesh;


        public BoxSO ()
		{
			width = height = depth = 3.0f;

            Parameters.Register(
                "width", () => { return Width; }, (f) => { Width = f; }, width);
            Parameters.Register(
                "height", () => { return Height; }, (f) => { Height = f; }, height);
            Parameters.Register(
                "depth", () => { return Depth; }, (f) => { Depth = f; }, depth);
            Parameters.Register(
                "scaled_width", () => { return ScaledWidth; }, (f) => { ScaledWidth = f; }, height);
            Parameters.Register(
                "scaled_height", () => { return ScaledHeight; }, (f) => { ScaledHeight = f; }, height);
            Parameters.Register(
                "scaled_depth", () => { return ScaledDepth; }, (f) => { ScaledDepth = f; }, height);


        }

        public BoxSO Create( SOMaterial defaultMaterial ) {
            AssignSOMaterial(defaultMaterial);       // need to do this to setup BaseSO material stack

            box = new GameObject( UniqueNames.GetNext("Box") );
			boxMesh = AppendUnityPrimitiveGO ("boxMesh", PrimitiveType.Cube, CurrentMaterial, box);
			boxMesh.transform.localScale = new Vector3 (width, height, depth);

            update_shift();
            box.transform.position += centerShift;

            increment_timestamp();
            return this;
        }



        // to reposition relative to desired center point we need to shift
        // object, but need to be able to undo this shift after changing params
        Vector3 centerShift;
        void update_shift()
        {
            centerShift = Vector3.zero;
            if (Center == CenterModes.Base)
                centerShift = new Vector3(0, 0.5f * height, 0);
            else if (Center == CenterModes.Corner)
                centerShift = new Vector3(0.5f * width, 0.5f * height, 0.5f * depth);
        }


        override public void UpdateGeometry()
        {
            if (box == null)
                return;

            boxMesh.GetComponent<MeshFilter>().sharedMesh = UnityUtil.GetPrimitiveMesh(PrimitiveType.Cube);
            boxMesh.transform.localScale = new Vector3(width, height, depth);

            // if we want to scale away/towards bottom of sphere, then we need to 
            //  translate along axis as well...
            Frame3f f = GetLocalFrame(CoordSpace.ObjectCoords);
            float fScale = box.transform.localScale[0];
            f.Origin -= f.FromFrameV((fScale * centerShift).ToVector3f());
            update_shift();
            f.Origin += f.FromFrameV((fScale * centerShift).ToVector3f());
            SetLocalFrame(f, CoordSpace.ObjectCoords);

            // apparently this is expensive?
            if (DeferRebuild == false) {
                boxMesh.GetComponent<MeshCollider>().sharedMesh = boxMesh.GetComponent<MeshFilter>().sharedMesh;
            }

            increment_timestamp();
        }



        public float Width {
            get { return width; }
            set {
                width = value;
                if (box != null)
                    UpdateGeometry();
            }
        }
        public float Height {
            get { return height; }
            set {
                height = value;
                if (box != null)
                    UpdateGeometry();
            }
        }
        public float Depth {
            get { return depth; }
            set {
                depth= value;
                if (box != null)
                    UpdateGeometry();
            }
        }


        public float ScaledWidth {
            get { return width * box.transform.localScale[0]; }
            set { Width = value / box.transform.localScale[0]; }
        }
        public float ScaledHeight {
            get { return height * box.transform.localScale[1]; }
            set { Height = value / box.transform.localScale[1]; }
        }
        public float ScaledDepth {
            get { return depth * box.transform.localScale[2]; }
            set { Depth = value / box.transform.localScale[2]; }
        }


        //
        // SceneObject impl
        //

        override public fGameObject RootGameObject
        {
            get { return box; }
        }

        override public string Name
        {
            get { return box.GetName(); }
            set { box.SetName(value); }
        }

        override public SOType Type { get { return SOTypes.Box; } }

        override public SceneObject Duplicate()
        {
            BoxSO copy = new BoxSO();
            copy.width = this.width;
            copy.height = this.height;
            copy.depth = this.depth;
            copy.Create(this.GetAssignedSOMaterial());
            copy.SetLocalFrame(
                this.GetLocalFrame(CoordSpace.ObjectCoords), CoordSpace.ObjectCoords);
            copy.SetLocalScale(this.GetLocalScale());
            return copy;
        }

        override public AxisAlignedBox3f GetLocalBoundingBox()
        {
            return new AxisAlignedBox3f(Vector3f.Zero, ScaledWidth / 2, ScaledHeight / 2, ScaledDepth / 2);
        }




        // SpatialQueryableSO impl

        public virtual bool SupportsNearestQuery { get { return true; } }
        public virtual bool FindNearest(Vector3d point, double maxDist, out SORayHit nearest, CoordSpace eInCoords)
        {
            nearest = null;

            // convert to local
            Vector3f local_pt = SceneTransforms.TransformTo((Vector3f)point, this, eInCoords, CoordSpace.ObjectCoords);

            AxisAlignedBox3f bounds = GetLocalBoundingBox();
            float local_dist = bounds.Distance(local_pt);
            float dist_incoords = SceneTransforms.TransformTo(local_dist, this, CoordSpace.ObjectCoords, eInCoords);

            if (dist_incoords > maxDist)
                return false;

            nearest = new SORayHit();
            nearest.fHitDist = dist_incoords;
            Vector3f nearPt = bounds.NearestPoint(local_pt);
            nearest.hitPos =  SceneTransforms.TransformTo(nearPt, this, CoordSpace.ObjectCoords, eInCoords);
            nearest.hitNormal = Vector3f.Zero;
            nearest.hitGO = RootGameObject;
            nearest.hitSO = this;
            return true;
        }




    }
}

