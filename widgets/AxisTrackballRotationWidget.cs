using System;
using f3;
using g3;
using GameObject = UnityEngine.GameObject;
using Debug = UnityEngine.Debug;

namespace f3
{

    /// <summary>
    /// this Widget implements rotation around an axis.
    /// 
    /// TODO: when axis is highly perp to ray, we should use cylinder instead of plane...
    /// </summary>
    public class AxisTrackballRotationWidget : Standard3DTransformWidget
    {
        private readonly int nRotationAxis;
        private readonly float rotateSpeed;

		public AxisTrackballRotationWidget(int nFrameAxis, float rotateSpeed = 1)
		{
			nRotationAxis = nFrameAxis;
			this.rotateSpeed = rotateSpeed;
		}

	    private Frame3f rotateFrameW;
        private Vector3f startHitWProjected;

        private static Vector3f GetBallIntersectionW(Vector3f originW, Ray3f rayW, double sphereRadius)
        {
            var mouseRay = (Ray3d) rayW;
            var origind = (Vector3d) originW;
            var intersection = IntersectionUtil.RaySphere(ref mouseRay.Origin, ref mouseRay.Direction, ref origind, sphereRadius);

            return intersection.intersects ? rayW.PointAt((float) intersection.parameter.a)
                : Frame3f.Identity.RayPlaneIntersection(rayW.Origin, rayW.Direction, 2);
        }

		public override bool BeginCapture(ITransformable target, Ray3f worldRay, UIRayHit hit)
		{
		    rotateFrameW = target.GetLocalFrame(CoordSpace.WorldCoords);
		    startHitWProjected = GetBallIntersectionW(rotateFrameW.Origin, worldRay, gizmoRadiusW);

            return true;
		}
        
        private Quaternionf GetCurrentRotation(Vector3f startIntersectionPointW, Vector3f intersectionPointW,
            Vector3f originW, Vector3f aroundW)
        {
            var radiusVectorOld = startIntersectionPointW - originW;
            var radiusVector = intersectionPointW - originW;
            
            Debug.DrawLine(originW.ToVector3(), startIntersectionPointW.ToVector3(), UnityEngine.Color.blue);
            Debug.DrawLine(originW.ToVector3(), intersectionPointW.ToVector3(), UnityEngine.Color.red);

            if (rotateSpeed == 1)
                return Quaternionf.FromToConstrained(radiusVectorOld, radiusVector, aroundW);

            Vector3f radiusVectorDiff = radiusVector - radiusVectorOld;
            return Quaternionf.FromToConstrained(radiusVectorOld, radiusVectorOld + rotateSpeed * radiusVectorDiff, aroundW);
        }

		public override bool UpdateCapture(ITransformable target, Ray3f worldRay)
		{
		    var hitW = GetBallIntersectionW(rotateFrameW.Origin, worldRay, gizmoRadiusW);

            var rotateAxisW = rotateFrameW.GetAxis(nRotationAxis);
		    var rotation = GetCurrentRotation(startHitWProjected, hitW, rotateFrameW.Origin, rotateAxisW);
		    
			// update target
			target.SetLocalFrame (rotateFrameW.Rotated(rotation), CoordSpace.WorldCoords);

            return true;
		}

        public override bool EndCapture(ITransformable target)
        {
            return true;
        }

        public override void Disconnect()
        {
            RootGameObject.Destroy();
        }


        private static readonly double VisibilityThresh = Math.Cos(85 * MathUtil.Deg2Rad);

        public override bool CheckVisibility(ref Frame3f curFrameW, ref Vector3d eyePosW)
        {
            Vector3d axis = curFrameW.GetAxis(nRotationAxis);
            var eyevec = (eyePosW - curFrameW.Origin).Normalized;
            var dot = axis.Dot(eyevec);
            return Math.Abs(dot) > VisibilityThresh;
        }
    }
}

