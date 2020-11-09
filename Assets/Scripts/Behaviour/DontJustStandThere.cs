
using UnityEngine;

namespace Behaviour 
{
    public class DontJustStandThere : MonoBehaviour
    {
        public Vector3 thisWay = Vector3.left;
        public float   stepAside = 4F;

        public Vector3 rotateThisWayPlease = Vector3.forward;
        public float   getAlongNow = 4F;
        // Start is called before the first frame update

        // Update is called once per frame
        void Update()
        {
            transform.position += thisWay * (stepAside * Time.deltaTime);
            transform.localRotation *= Quaternion.Euler(
                rotateThisWayPlease * (getAlongNow * Time.deltaTime)
                );
        }
    }
        
}
