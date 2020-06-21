#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BashCommandExecutor
{

    [MenuItem("Suntabu/BashExecutorTest")]
    public static void TestExecute()
    {
        Execute(new []{"git --version","\n","\n","pwd","\n","\n","\n","ls -al"});
    }
    
    
    public static void Execute(string[] commands)
    {
        System.Diagnostics.Process process = new System.Diagnostics.Process();
        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            FileName = "/bin/bash",
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
        };
        process.StartInfo = startInfo;
        process.Start();
        using (var sw = process.StandardInput)
        {
            foreach (var command in commands)
            {
                sw.WriteLine(command);
            }
        }
        
        
        process.WaitForExit();
        int exitCode = process.ExitCode;
        if (exitCode != 0)
        {
            Debug.LogError("Run prebuild.command Failed : " + exitCode + "  " +
                           process.StandardError.ReadToEnd() + "\n");
        }
        else
        {
            Debug.Log(process.StandardOutput.ReadToEnd());
        }

    }
}

#endif