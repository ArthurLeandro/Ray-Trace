using UnityEngine;
using System.Collections;

public class ObjectRayTracingInfo : MonoBehaviour {
  #region Coeficients
   public float lambertCoefficient = 1f;
   public float reflectiveCoefficient = 0f;
  public float transparentCoefficient = 0f;
  #endregion
  #region Phong
  public float phongCoefficient = 1f;
  public float phongPower = 2f;
  #endregion
  #region Blinn
  public float blinnPhongCoefficient = 1f;
  public float blinnPhongPower = 2f;
  #endregion
  public Color defaultColor = Color.gray;
  void Awake() {
    // Assign the material if non exists.
    if (!GetComponent<Renderer>().material.mainTexture) {
      GetComponent<Renderer>().material.color = defaultColor;
    }
  }
}