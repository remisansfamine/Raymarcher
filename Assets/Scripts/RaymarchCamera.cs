using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class RaymarchCamera : SceneViewFilter
{
	[SerializeField]
	private Shader shader = null;

	public Material raymarchMaterial
	{
		get
		{
			if (!raymarchMat && shader)
			{
				raymarchMat = new Material(shader);
				raymarchMat.hideFlags = HideFlags.HideAndDontSave;
			}
			return raymarchMat;
		}
	}
	private Material raymarchMat;

	public Camera _camera
	{
		get
		{
			if (!_cam)
			{
				_cam = GetComponent<Camera>();
			}
			return _cam;
			
		}
	}
	private Camera _cam;

	public float maxDistance;

	[Header("Setup")]
	[Min(1)]
	public int			maxIterations;
	[Range(0.1f, 0.001f)]
	public float		accuracy;

	[Header("Directional Light")]
	public Light		directionalLight;
	public Color		lightColor;

	[Header("Shadow")]
	[Range(0, 128)]
	public float		shadowIntensity;
	public Vector2		shadowDistance;
	[Range(1, 128)]
	public float		shadowPenumbra;

	[Header("Ambient Occlusion")]
	[Range(0.01f, 10.0f)]
	public float		aoStepsize;
	[Range(1,5)]
	public int			aoIterations;
	[Range(0,1)]
	public float		aoIntensity;

	[Header("Reflection")]
	[Range(0, 10)]
	public int			reflectionCount;
	[Range(0, 5)]
	public float		reflectionIntensity;
	[Range(0, 5)]
	public float		envReflIntensity;
	public Cubemap		reflectionCube;

	[Header("Signed Distance Field")]
	public Vector4		sphere;
	[Min(0)]
	public float		sphereSmooth;
	public float		degreeRotate;

	[Header("Color")]
	public Color		groundColor;
	public Gradient		sphereGradient;
	private Color[]		sphereColor = new Color[8];
	[Range(0, 4)]
	public float		colorIntensity;



	/*public Vector4 sphere1;
	public Vector4 box1;
	public float boxRound;
	public float boxSphereSmooth;
	public Vector4 sphere2;
	public float sphereIntersectSmooth;*/
	public Vector3 modInterval;

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (!raymarchMaterial)
		{
			Graphics.Blit(source, destination);
			return;
		}

		for (int i = 0; i < 8; i++)
		{
			sphereColor[i] = sphereGradient.Evaluate(i / 8f);
		}

		raymarchMaterial.SetColor("groundColor", groundColor);
		raymarchMaterial.SetColorArray("sphereColor", sphereColor);
		raymarchMaterial.SetFloat("colorIntensity", colorIntensity);

		// Setup
		raymarchMaterial.SetMatrix("CamFrustum", CamFrustum(_camera));
		raymarchMaterial.SetMatrix("CamToWorld", _camera.cameraToWorldMatrix);

		raymarchMaterial.SetFloat("maxDistance", maxDistance);
		raymarchMaterial.SetInt("maxIterations", maxIterations);
		raymarchMaterial.SetFloat("accuracy", accuracy);

		// Light
		raymarchMaterial.SetVector("lightDir", directionalLight ? directionalLight.transform.forward : Vector3.down);
		raymarchMaterial.SetColor("lightCol", directionalLight.color);
		raymarchMaterial.SetFloat("lightIntensity", directionalLight.intensity);

		// Shadow
		raymarchMaterial.SetFloat("shadowIntensity", shadowIntensity);
		raymarchMaterial.SetFloat("shadowPenumbra", shadowPenumbra);
		raymarchMaterial.SetVector("shadowDistance", shadowDistance);
		
		// SDF
		raymarchMaterial.SetVector("sphere", sphere);
		raymarchMaterial.SetFloat("sphereSmooth", sphereSmooth);
		raymarchMaterial.SetFloat("degreeRotate", degreeRotate);

		/*raymarchMaterial.SetFloat("boxRound", boxRound);
		raymarchMaterial.SetFloat("boxSphereSmooth", boxSphereSmooth);
		raymarchMaterial.SetFloat("sphereIntersectSmooth", sphereIntersectSmooth);
		raymarchMaterial.SetVector("sphere1", sphere1);
		raymarchMaterial.SetVector("sphere2", sphere2);
		raymarchMaterial.SetVector("box1", box1);*/
		
		raymarchMaterial.SetVector("modInterval", modInterval);

		// Ambiant Occlusion
		raymarchMaterial.SetFloat("aoStepsize", aoStepsize);
		raymarchMaterial.SetFloat("aoIntensity", aoIntensity);
		raymarchMaterial.SetInt("aoIterations", aoIterations);

		// Reflection
		raymarchMat.SetInt("reflectionCount", reflectionCount);
		raymarchMat.SetFloat("reflectionIntensity", reflectionIntensity);
		raymarchMat.SetFloat("envReflIntensity", envReflIntensity);
		raymarchMat.SetTexture("reflectionCube", reflectionCube);

		RenderTexture.active = destination;
		raymarchMaterial.SetTexture("_MainTex", source);

		GL.PushMatrix();
		GL.LoadOrtho();
		raymarchMaterial.SetPass(0);
		GL.Begin(GL.QUADS);

		// BL
		GL.MultiTexCoord2(0, 0.0f, 0.0f);
		GL.Vertex3(0.0f, 0.0f, 3.0f);

		// BR
		GL.MultiTexCoord2(0, 1.0f, 0.0f);
		GL.Vertex3(1.0f, 0.0f, 2.0f);

		// TR
		GL.MultiTexCoord2(0, 1.0f, 1.0f);
		GL.Vertex3(1.0f, 1.0f, 1.0f);

		// TL
		GL.MultiTexCoord2(0, 0.0f, 1.0f);
		GL.Vertex3(0.0f, 1.0f, 0.0f);

		GL.End();
		GL.PopMatrix();
	}

	private Matrix4x4 CamFrustum(Camera cam)
	{
		Matrix4x4 frustum = Matrix4x4.identity;

		float fov = Mathf.Tan((cam.fieldOfView * 0.5f) * Mathf.Deg2Rad);

		Vector3 goUp = Vector3.up * fov;
		Vector3 goRigth = Vector3.right * fov * cam.aspect;

		Vector3 TL = (-Vector3.forward - goRigth + goUp);
		Vector3 TR = (-Vector3.forward + goRigth + goUp);
		Vector3 BR = (-Vector3.forward + goRigth - goUp);
		Vector3 BL = (-Vector3.forward - goRigth - goUp);

		frustum.SetRow(0, TL);
		frustum.SetRow(1, TR);
		frustum.SetRow(2, BR);
		frustum.SetRow(3, BL);

		return frustum;
	}
}
