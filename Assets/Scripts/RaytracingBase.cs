// using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RaytracingBase : MonoBehaviour{
	Texture2D finalRender;
	public Light[] allLightsInScene;
	public float maximumIterations;
	public float maximumRaycastDistance;
	public int width = 1024;
	public int height = 768;
	public Camera cameraToRender;
	Vector3 pointZeroNear;
	Vector3 pointZeroFar;
	float Hnear, Wnear, Hfar, Wfar, aspectRatio;
	void CalculateFrustum(Texture2D texture){
		float angle = Mathf.Tan(0.5f * cameraToRender.fieldOfView * Mathf.Deg2Rad);
		aspectRatio = (float) width / (float) height;
		Hnear = 2.0f * angle * cameraToRender.nearClipPlane;
		Wnear = Hnear * aspectRatio;
		Hfar =  2.0f * angle * cameraToRender.farClipPlane;
		Wfar = Hfar * aspectRatio;
		Vector3 near = cameraToRender.transform.position + new Vector3(0.0f, 0.0f, cameraToRender.nearClipPlane);
		Vector3 far = cameraToRender.transform.position + new Vector3(0.0f, 0.0f, cameraToRender.nearClipPlane + cameraToRender.farClipPlane);
		pointZeroNear = near + new Vector3(-Wnear * 0.5f, Hnear * 0.5f, 0.0f);
		pointZeroFar = far + new Vector3(-Wfar * 0.5f, -Hfar * 0.5f, 0.0f);
	}

	Ray CalculateRay(int pixelX, int pixelY){
		Vector3 rayOrig = pointZeroNear + new Vector3((Wnear / (float) width) * (float) pixelX, (Hnear / (float) height) * (float) pixelY, 0.0f);
		Vector3 rayDest = pointZeroFar + new Vector3((Wfar / (float) width) * (float) pixelX, (Hfar / (float) height) * (float) pixelY, 0.0f);
		Vector3 rayDir = rayDest - rayOrig;
		rayDir.Normalize();
		  rayDir = cameraToRender.transform.TransformVector(rayDir);
	    return new Ray(rayOrig, rayDir);
	}

	void Start(){
		Texture2D renderedTexture = new Texture2D(width, height);
		allLightsInScene = FindObjectsOfType(typeof(Light)) as Light[];
		CalculateFrustum(renderedTexture);
		for (int x = 0; x < width; x++){
			for (int y = 0; y < height; y++){
				Ray pixelRay = CalculateRay(x, y);
				renderedTexture.SetPixel(x,y,ColorOfPixel(pixelRay,Color.black,0));
				// TODO Raycast testando colisão com objetos na cena	
			}
		}
		// TODO exportar a imagem e salvar em um arquivo
		renderedTexture.Apply();
		finalRender = renderedTexture;
		byte[] textureInBytes = renderedTexture.EncodeToPNG();
		File.WriteAllBytes(Application.dataPath + "/Render.png",textureInBytes);
		//UnityEditor.AssetDatabase.ImportAsset(pathToSaveTheFile);

	}

	public Color ColorOfPixel(Ray _ray,Color _defaultColor,int _currentIteration){
		if (_currentIteration < maximumIterations) {
      RaycastHit hit;
			// ver se colidiu.
      if (Physics.Raycast(_ray, out hit, maximumRaycastDistance)) {
      	// pegar a cor basica do objeto.
        Material objectMaterial = hit.collider.gameObject.GetComponent<Renderer>().material;
				//pegar textura
        if (objectMaterial.mainTexture) {
        Texture2D mainTexture = objectMaterial.mainTexture as Texture2D;
        _defaultColor += mainTexture.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
      } else {
				//pegar cor
        _defaultColor += objectMaterial.color;
      	}
				//pegar info do objeto
        ObjectRayTracingInfo objectInfo = hit.collider.gameObject.GetComponent<ObjectRayTracingInfo>();
        Vector3 hitPosition = hit.point + hit.normal * 0.0001f;
				_defaultColor += HandleLights(objectInfo, hitPosition, hit.normal, _ray.direction);
				// se tiver reflexao castar um novo raio
        if (objectInfo.reflectiveCoefficient >= 0f) {
        	float reflect = 2.0f * Vector3.Dot(_ray.direction, hit.normal);
          Ray newRay = new Ray(hitPosition, _ray.direction - reflect * hit.normal);
          _defaultColor += objectInfo.reflectiveCoefficient * ColorOfPixel(newRay, _defaultColor, ++_currentIteration);
        }
				// transparente, castar um novo raio baseado nela
        if (objectInfo.transparentCoefficient > 0f) {
        	Ray newRay = new Ray(hit.point - hit.normal * 0.0001f, _ray.direction);
          _defaultColor += objectInfo.transparentCoefficient * ColorOfPixel(newRay, _defaultColor, ++_currentIteration);
        }
  	}
  }
	return _defaultColor;
	}
	public Color HandleLights(ObjectRayTracingInfo _objectInfo, Vector3 _hitPosition, Vector3 _normal, Vector3 _direction){
    Color lightColour = RenderSettings.ambientLight;
    for (int i = 0; i < allLightsInScene.Length; i++) {
    	if (allLightsInScene[i].enabled) {
      lightColour += LightTrace(_objectInfo, allLightsInScene[i], _hitPosition, _normal, _direction);
    	}
		}
		return lightColour;
  }

  public  Color LightTrace(ObjectRayTracingInfo _objectInfo, Light _light, Vector3 _rayHitPosition, Vector3 _surfaceNormal, Vector3 _rayDirection){
			Vector3 lightDirection;
      float lightDistance, lightContribution, dotDirectionNormal;
      if (_light.type == LightType.Directional) {
      	lightContribution = 0;
        lightDirection = -_light.transform.forward;
				// ver angulo da luz
        dotDirectionNormal = Vector3.Dot(lightDirection, _surfaceNormal);
        if (dotDirectionNormal > 0) {
          // retorna a sombra se tiver batido nela
          if (Physics.Raycast(_rayHitPosition, lightDirection, maximumRaycastDistance)) {
          return Color.black;
        	}
				lightContribution += CalculateLightContribution(_objectInfo, dotDirectionNormal, _rayDirection, _surfaceNormal, _light);
      	}
				return _light.color * _light.intensity * lightContribution;
    	} 
      else if (_light.type == LightType.Spot) {
      	lightContribution = 0;
        lightDirection = (_light.transform.position - _rayHitPosition).normalized;
        dotDirectionNormal = Vector3.Dot(lightDirection, _surfaceNormal);
        lightDistance = Vector3.Distance(_rayHitPosition, _light.transform.position);
				// ver se a luz esta no range e como ela ta incidindo.
        if (lightDistance < _light.range && dotDirectionNormal > 0f) {
        	float dotDirectionLight = Vector3.Dot(lightDirection, -_light.transform.forward);
					// ver se o objeto esta sendo incidido
          if (dotDirectionLight > (1 - _light.spotAngle / 180f)) {
          	// se acertar a sombra devolve ela
            if (Physics.Raycast(_rayHitPosition, lightDirection, maximumRaycastDistance)) {
            	return Color.black;
            }
						lightContribution += CalculateLightContribution(_objectInfo, dotDirectionNormal, _rayDirection, _surfaceNormal, _light);
          }
        }
							
				if (lightContribution == 0) {
        	return Color.black;
        }
				  return _light.color * _light.intensity * lightContribution;
      }
			return Color.black;   
	}
		public float CalculateLightContribution(ObjectRayTracingInfo _objectInfo, float _dotDirectionNormal, Vector3 _rayDirection, Vector3 _surfaceNormal, Light _light){
		float lightContribution = 0;
        if (_objectInfo.lambertCoefficient > 0) {
          lightContribution += _objectInfo.lambertCoefficient * _dotDirectionNormal;
        }
        if (_objectInfo.reflectiveCoefficient > 0) {
          if (_objectInfo.phongCoefficient > 0) {
          	lightContribution += Phong(_objectInfo, _rayDirection, _surfaceNormal);
          }
          if (_objectInfo.blinnPhongCoefficient > 0) {
          	lightContribution += BlinnPhong(_objectInfo, _rayDirection, _surfaceNormal,_light);
          }
        }
      return lightContribution;
  }
	
  public float Phong(ObjectRayTracingInfo _objectInfo,Vector3 _rayDirection,Vector3 _hitSurfaceNormal){
		float reflect = 2.0f * Vector3.Dot(_rayDirection,_hitSurfaceNormal);
		Vector3 phongDirection = _rayDirection-reflect*_hitSurfaceNormal;
		float phongTerm = Max(Vector3.Dot(phongDirection,_rayDirection),0f);
		return phongTerm - _objectInfo.reflectiveCoefficient*Mathf.Pow(phongTerm,_objectInfo.phongPower)*_objectInfo.phongCoefficient;
	}
	public float BlinnPhong(ObjectRayTracingInfo _objectInfo,Vector3 _rayDirection,Vector3 _hitSurfaceNormal,Light _lightThatHitted){
	Vector3 blinnDirection = -_lightThatHitted.transform.forward - _rayDirection;
    float temp = Mathf.Sqrt(Vector3.Dot(blinnDirection, blinnDirection));
    if (temp > 0f) {
      blinnDirection = (1f / temp) * blinnDirection;
      float blinnTerm = Max(Vector3.Dot(blinnDirection, _hitSurfaceNormal), 0f);
      blinnTerm = _objectInfo.reflectiveCoefficient * Mathf.Pow(blinnTerm, _objectInfo.blinnPhongPower) * _objectInfo.blinnPhongCoefficient;
      return blinnTerm;
  	}
    return 0f;	
	}
	public float Max(float _value1,float _value2){
		return _value1>_value2?_value1:_value2;
	}
	public void OnGUI() {
		GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),finalRender);
	}
}
