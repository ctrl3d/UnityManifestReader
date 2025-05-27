using UnityEngine;
using work.ctrl3d.UnityManifestReader;

public class TestUnityManifestReader : MonoBehaviour
{
    private void Start()
    {
        var packages = UnityManifestReader.GetAllPackages();
        
        foreach (var package in packages)
        {
            Debug.Log(package);
        }
        
        //UnityManifestReader.PrintPackages();
        

    }
 }
