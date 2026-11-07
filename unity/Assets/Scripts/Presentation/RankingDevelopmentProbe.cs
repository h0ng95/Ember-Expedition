#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Expedition.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using UnityEngine;
using UnityEngine.Networking;

namespace Expedition.Presentation
{
    // Opt-in, development-build-only smoke test. Uses the game's normal signed-in SDK session.
    // Never reads stored credentials or changes the player's expedition or cloud backup.
    public sealed class RankingDevelopmentProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Attach()
        {
            if(File.Exists(Path.Combine(Application.persistentDataPath,"ranking-probe.request")))
                new GameObject("RankingDevelopmentProbe").AddComponent<RankingDevelopmentProbe>();
        }
        async void Start()
        {
            string request=Path.Combine(Application.persistentDataPath,"ranking-probe.request");
            string output=Path.Combine(Application.persistentDataPath,"ranking-probe-result.json");
            File.Move(request,request+".started-"+DateTime.UtcNow.Ticks);
            var report=new List<string>();
            try
            {
                for(int i=0;i<120 && !UgsAccountBackend.Linked;i++)await Task.Delay(500);
                if(!UgsAccountBackend.Linked)throw new InvalidOperationException("Normal Google session unavailable");
                var top=await Call<RankingPage>("GetRanking");report.Add("Google read: PASS");
                string requestId=Guid.NewGuid().ToString("N");
                var start=new Dictionary<string,object>{{"formation","standard"},{"requestId",requestId}};
                var ticket=await Call<RankedRunTicket>("StartRun",start);
                var retry=await Call<RankedRunTicket>("StartRun",start);
                if(ticket.Id!=retry.Id || ticket.Seed==0)throw new Exception("Ticket retry mismatch");
                report.Add("Start retry: PASS");
                var db=CatalogDatabase.FromJson(Resources.Load<TextAsset>("Content/unit-catalog-v010").text);
                var local=ExpeditionRun.New(db,ticket.Seed,startFormationId:ticket.Formation);local.AttachRankedTicket(ticket);
                while(!local.HasEnded)
                {
                    await Task.Run(()=>RankedReplay.Fight(local));
                    if(!local.HasEnded)local.Continue();
                }
                var proof=local.RankedProof;
                var forged=proof.Copy();foreach(var command in forged.Commands)if(command.Kind=="fight"){command.Flag=!command.Flag;break;}
                bool rejected=false;
                try{await Call<int>("SubmitRun",new Dictionary<string,object>{{"proofJson",JsonUtility.ToJson(forged)}});}
                catch(CloudCodeException){rejected=true;}
                if(!rejected)throw new Exception("Forged victory accepted");report.Add("Forged victory rejected: PASS");
                var arguments=new Dictionary<string,object>{{"proofJson",JsonUtility.ToJson(proof)}};
                int score=await Call<int>("SubmitRun",arguments), duplicate=await Call<int>("SubmitRun",arguments);
                if(score!=local.Score || duplicate!=score)throw new Exception("Server score differs from Android simulation");
                report.Add("Android/server replay and submit retry: PASS; score="+score);
                top=await Call<RankingPage>("GetRanking");
                if(top.Mine==null || top.Mine.Score<score)throw new Exception("Own score missing");
                report.Add("Own rank and top list: PASS");
                string board="https://leaderboards.services.api.unity.com/v1/projects/"+Application.cloudProjectId+
                    "/leaderboards/normal_r6_v1/scores";
                string token=AuthenticationService.Instance.AccessToken;
                var read=await Http(board,"GET",null,token);
                var write=await Http(board+"/players/"+AuthenticationService.Instance.PlayerId,"POST","{\"score\":999999}",token);
                if(read.Code!=403 || write.Code!=403)throw new Exception("Direct player board access not denied");
                report.Add("Direct read/write denied: PASS");
                var anonymous=await Http("https://player-auth.services.api.unity.com/v1/authentication/anonymous","POST","{}",null);
                var session=JsonUtility.FromJson<ProbeSession>(anonymous.Body);
                if(anonymous.Code!=200 || string.IsNullOrEmpty(session.idToken))throw new Exception("Anonymous control unavailable");
                foreach(string endpoint in new[]{"GetRanking","StartRun","SubmitRun"})
                {
                    string args=endpoint=="StartRun"?"{\"params\":{\"formation\":\"standard\",\"requestId\":\""+Guid.NewGuid().ToString("N")+"\"}}":
                        endpoint=="SubmitRun"?"{\"params\":{\"proofJson\":\"{}\"}}":"{\"params\":{}}";
                    var denied=await Http("https://cloud-code.services.api.unity.com/v1/projects/"+Application.cloudProjectId+
                        "/modules/IgnisRankings/"+endpoint,"POST",args,session.idToken);
                    if(denied.Code==200)throw new Exception("Anonymous endpoint accepted");
                    report.Add("Anonymous "+endpoint+" rejected: PASS ("+denied.Code+")");
                }
                report.Add("ALL_PASS");
            }
            catch(Exception ex)
            {
                // Only a sanitized type/error code; no exception body, user IDs or tokens.
                report.Add("FAIL: "+ex.GetType().Name+(ex is CloudCodeException cc ? " code="+cc.ErrorCode : ""));
            }
            File.WriteAllText(output,JsonUtility.ToJson(new ProbeReport{Checks=report.ToArray()},true));
            Destroy(gameObject);
        }
        [Serializable] sealed class ProbeSession { public string idToken; }
        [Serializable] sealed class ProbeReport { public string[] Checks; }
        sealed class ProbeResponse { public long Code; public string Body; }
        static Task<T> Call<T>(string endpoint,Dictionary<string,object> args=null) =>
            CloudCodeService.Instance.CallModuleEndpointAsync<T>("IgnisRankings",endpoint,args??new Dictionary<string,object>());
        static async Task<ProbeResponse> Http(string url,string method,string body,string token)
        {
            using(var request=new UnityWebRequest(url,method))
            {
                request.downloadHandler=new DownloadHandlerBuffer();request.timeout=25;
                request.SetRequestHeader("Content-Type","application/json");
                request.SetRequestHeader("ProjectId",Application.cloudProjectId);
                request.SetRequestHeader("UnityEnvironment",CloudAccountConfig.Load().EnvironmentName);
                if(token!=null)request.SetRequestHeader("Authorization","Bearer "+token);
                if(body!=null)request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                var operation=request.SendWebRequest();while(!operation.isDone)await Task.Delay(20);
                return new ProbeResponse{Code=request.responseCode,Body=request.downloadHandler.text};
            }
        }
    }
}
#endif
