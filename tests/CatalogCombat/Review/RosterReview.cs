using System;
using System.IO;
using Expedition.Core;
class RosterReview {
 static int assertions,failures;
 static void Check(bool ok,string message){assertions++;Console.WriteLine((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;}
 static int Main(string[] args){
 var db=CatalogDatabase.FromJson(File.ReadAllText(args[0]));Check(db.All.Count==30,"catalog has 30 forms");
 var r=new CatalogRoster(db);r.Recruit("receiver","B-KN",1,2);r.Recruit("donor","B-PR",2,3);r.Transfer("donor","receiver",true);r.Recruit("later","B-AR",3,4);r.Undo();Check(r.Find("later")!=null&&r.SupplyUsed==3,"Undo preserves later summon and supply count");
 r=new CatalogRoster(db);r.Recruit("receiver","B-KN",1,2);for(int i=0;i<6;i++){r.Recruit("d"+i,"B-PR",0,0);r.Transfer("d"+i,"receiver",false);}Check(r.Find("receiver").Growth==2,"Basic growth capped at 2 until branch");r.Evolve("receiver","E-KN-C");Check(!r.Evolve("receiver","F-KN"),"Final requires 4 donors after branch");
 string[][] recipes={new[]{"KN","FI"},new[]{"KN","PR"},new[]{"AR","IC"},new[]{"FI","IC"},new[]{"PR","BA"},new[]{"AR","BA"}};
 string[][] branches={new[]{"KN","G","C"},new[]{"AR","P","T"},new[]{"FI","B","E"},new[]{"PR","G","H"},new[]{"IC","F","S"},new[]{"BA","G","A"}};
 for(int h=0;h<6;h++) for(int first=1;first<=2;first++)for(int second=1;second<=2;second++){
 string f1=recipes[h][0],f2=recipes[h][1],s1=null,s2=null;foreach(var pair in branches){if(pair[0]==f1)s1=pair[first];if(pair[0]==f2)s2=pair[second];}
 r=new CatalogRoster(db);r.Recruit("a","B-"+f1,1,2);r.Recruit("b","B-"+f2,3,4);for(int i=0;i<4;i++)r.Recruit("d"+i,"B-PR",0,0);r.Transfer("d0","a",false);r.Transfer("d1","a",false);r.Transfer("d2","b",false);r.Transfer("d3","b",false);r.Evolve("a","E-"+f1+"-"+s1);r.Evolve("b","E-"+f2+"-"+s2);Check(r.Combine("a","b","H-"+(h+1).ToString("00"))&&r.Members.Count==1&&r.Find("a").X==1&&r.Find("a").Y==2,"fusion "+(h+1)+" variants "+first+second);Check(r.Undo()&&r.Members.Count==2&&r.Find("a").FormId=="E-"+f1+"-"+s1,"fusion undo restores original forms");
 }
 foreach(var pair in branches)for(int v=1;v<=2;v++){r=new CatalogRoster(db);r.Recruit("a","B-"+pair[0],1,2);for(int i=0;i<6;i++)r.Recruit("d"+i,"B-PR",0,0);r.Transfer("d0","a",false);r.Transfer("d1","a",false);string branch="E-"+pair[0]+"-"+pair[v];r.Evolve("a",branch);for(int i=2;i<6;i++)r.Transfer("d"+i,"a",false);Check(r.Evolve("a","F-"+pair[0])&&r.Find("a").BranchId==branch&&r.SupplyUsed==7,"final retains "+branch);}
 Console.WriteLine("ROSTER REVIEW "+assertions+" assertions; "+failures+" failures");
 return failures == 0 ? 0 : 1;
 }
}
