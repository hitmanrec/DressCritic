using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DressmakerCritic.Diagnostics;

static class JuryTests
{
    public static string Good => Reply(7,6,8,7);
    public static string UnknownContext => Context();
    static string Reply(double? a, double? b, double? c, double? d)
    {
        var scores = new[] { a,b,c,d };
        return JsonSerializer.Serialize(new { schemaVersion = "2.0.0", jury = JuryContract.HouseIds.Select((id,i) => new { houseId = id, status = scores[i].HasValue ? "assessed" : "unknown", score = scores[i], remarks = new[] { "Fixture observation of a visible design decision." } }), limitations = new[] { "One view only." } });
    }
    static string Context(string orderStatus = "unknown", double? orderScore = null, string customerStatus = "unknown", double? customerScore = null) => JsonSerializer.Serialize(new {
        schemaVersion = "2.0.0",
        orderCompliance = new { status = orderStatus, scope = "provided-summary-only", score = orderScore, remarks = new[] { "Fixture contextual observation." } },
        customerCompatibility = new { status = customerStatus, scope = "verified-customer-preferences-only", score = customerScore, remarks = new[] { "Fixture preference observation." } }, limitations = new string[0] });
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static void Refuse(Action action, string label) { bool failed=false;try{action();}catch{failed=true;}Check(failed,label); }
    public static void Run()
    {
        Refuse(()=>JuryContract.ReadJury(" ","fixture"), "empty assessment rejected explicitly");
        var result = JuryContract.ReadJury(Good,"fixture");
        Check(result.jury.Count==4 && result.summary.average==7 && result.summary.verdict=="strong" && result.summary.divided==false, "four-house arithmetic mean and verdict computed by application");
        var divided=JuryContract.ReadJury(Reply(10,6,5,4),"fixture");
        Check(divided.summary.average==6.25 && divided.summary.spread==6 && divided.summary.divided==true, "jury disagreement is preserved separately from arithmetic mean");
        var partial=JuryContract.ReadJury(Reply(7,null,8,7),"fixture");
        Check(partial.summary.average==null && partial.summary.status=="insufficient-evidence", "missing critic does not become zero or a three-critic mean");
        Check(JuryContract.ReadJury(Reply(0,0,0,0),"fixture").summary.average==0 && JuryContract.ReadJury(Reply(10,10,10,10),"fixture").summary.average==10, "zero and ten are valid score endpoints");
        Refuse(()=>JuryContract.ReadJury(Reply(7.25,6,8,7),"fixture"),"jury scores restricted to one decimal");
        Refuse(()=>JuryContract.ReadJury(Good.Replace("\"character\"","\"harmony\""),"fixture"),"duplicate/missing house rejected");
        Refuse(()=>JuryContract.ReadJury(Good.Replace("\"limitations\":[","\"average\":99,\"limitations\":["),"fixture"),"model cannot supply or alter aggregate");
        Refuse(()=>JuryContract.ReadJury(Good.Replace("Fixture observation of a visible design decision.", "One short evidence-based sentence."),"fixture"), "copied real-capture placeholder is not a valid assessment");
        var input=new EvaluationInput();
        Check(!JuryContract.NeedsContext(input),"sale garment needs no order or customer request");
        Refuse(()=>JuryContract.ApplyContext(Context("consistent",9),input,result),"unknown order cannot receive contextual score");
        Refuse(()=>JuryContract.ApplyContext(Context(customerStatus:"consistent",customerScore:9),input,result),"unknown customer cannot get invented preferences");
        input.order.association="explicit accepted-order index 0";input.order.definition=new AssetIdentity{status="fields",prettyName="ORDER_SECRET"};
        input.order.brief.status="verified-guid";input.order.brief.summary="BRIEF_SECRET";input.order.brief.sourceUrl="https://source.invalid/brief";
        JuryContract.ApplyContext(Context("consistent",9.5),input,result);
        Check(result.orderCompliance.score==9.5 && result.summary.average==7,"order score never changes jury mean");
        input.customer.status="verified";input.customer.customerId="CUSTOMER_ID_SECRET";input.customer.source="SOURCE_SECRET";input.customer.preferences.Add("CUSTOMER_SECRET");
        JuryContract.ApplyContext(Context("inconsistent",2,"consistent",8),input,result);
        Check(result.customerCompatibility.score==8 && result.summary.average==7,"customer compatibility never changes jury mean");
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
        Refuse(() => JuryContract.ReadJury(File.ReadAllText(Path.Combine(root, "tests/fixtures/qwen35-2b-probe/invalid-jury-content.json")), "real-2b"),
            "real 2B output with numeric statuses and missing version is not silently repaired into a jury assessment");
        string stringRemarks = File.ReadAllText(Path.Combine(root,"tests/fixtures/jury-string-remarks.json"));
        Refuse(() => JuryContract.ReadJury(stringRemarks, "strict"), "canonical assessment schema still requires remarks arrays");
        string normalized = LocalEvaluator.NormalizeRemarks(stringRemarks, EvaluationTask.Jury);
        var recovered = JuryContract.ReadJury(normalized, "230842");
        Check(recovered.summary.average == 6.625 && recovered.jury.All(v => v.remarks.Count == 1), "real 0.15.3 string remarks recover all four votes without changing their scores");
        using (var original = JsonDocument.Parse(stringRemarks))
            Check(recovered.jury.Select(v => v.remarks[0]).SequenceEqual(original.RootElement.GetProperty("jury").EnumerateArray().Select(v => v.GetProperty("remarks").GetString())), "normalization preserves complete remarks verbatim");
        Check(LocalEvaluator.NormalizeRemarks(Good, EvaluationTask.Jury) == Good, "canonical response is returned unchanged");
        foreach (string value in new[] { "", new string('x', 241), "One short evidence-based sentence." })
        {
            var bad = System.Text.Json.Nodes.JsonNode.Parse(stringRemarks);
            bad["jury"][0]["remarks"] = value;
            Refuse(() => JuryContract.ReadJury(LocalEvaluator.NormalizeRemarks(bad.ToJsonString(), EvaluationTask.Jury), "bad"), "string compatibility keeps length/nonempty/placeholder validation");
        }
        var numeric = System.Text.Json.Nodes.JsonNode.Parse(stringRemarks);
        numeric["jury"][0]["remarks"] = 42;
        Refuse(() => JuryContract.ReadJury(LocalEvaluator.NormalizeRemarks(numeric.ToJsonString(), EvaluationTask.Jury), "bad"), "numeric remarks are not coerced to text");
        numeric["jury"][0]["remarks"] = "Visible detail.";
        numeric["jury"][0]["score"] = 11;
        Refuse(() => JuryContract.ReadJury(LocalEvaluator.NormalizeRemarks(numeric.ToJsonString(), EvaluationTask.Jury), "bad"), "normalization never repairs invalid scores");
        var contextual = System.Text.Json.Nodes.JsonNode.Parse(UnknownContext);
        contextual["orderCompliance"]["remarks"] = "No sourced order brief.";
        contextual["customerCompatibility"]["remarks"] = "No verified preferences.";
        var contextOnly = new JuryAssessment();
        JuryContract.ApplyContext(LocalEvaluator.NormalizeRemarks(contextual.ToJsonString(), EvaluationTask.Context), new EvaluationInput(), contextOnly);
        Check(contextOnly.orderCompliance.remarks.Single() == "No sourced order brief." && contextOnly.customerCompatibility.score == null, "context string remarks use same bounded compatibility without inventing scores");
        Refuse(()=>JuryContract.ReadJury(File.ReadAllText(Path.Combine(root,"tests/fixtures/capture-804/jury-content.json")),"real-fixture"), "exact first Instruct response with copied placeholders is rejected");
        var real = JuryContract.ReadJury(File.ReadAllText(Path.Combine(root,"tests/fixtures/capture-805/jury-content.json")),"real-success");
        Check(real.summary.average == 6.25 && real.summary.spread == 1.5 && real.summary.divided == false && real.summary.verdict == "competent", "first successful real model response preserves votes and computes correct aggregate");
        string png=Path.Combine(root,"tests/fixtures/transport.png");
        byte[] bytes=File.ReadAllBytes(png);
        input.garment=new GarmentSnapshot{garmentStatus="available",source="PlayerProgress.completedQuests[SECRET_INDEX].dress"};
        input.images.Add(new EvaluationImage{file="image.png",sha256=CaptureSession.Hash(png),width=2048,height=2048,binding="active-state-verified"});
        var fourInput = new EvaluationInput { garment = input.garment };
        for (int i=0;i<4;i++) fourInput.images.Add(new EvaluationImage { file="image.png",sha256=CaptureSession.Hash(png),width=2048,height=2048,binding="active-state-verified",view="axis-"+i });
        var four = ModelRequest.Build(fourInput,"fixture-model",_=>bytes);
        Check(four.messages[1].content.Count(p=>p.type=="image_url")==4 && four.messages[1].content.Count(p=>p.type=="text" && p.text.StartsWith("View:"))==4,"all four views have explicit adjacent orientation labels");
        fourInput.images.Add(fourInput.images[0]);
        Refuse(()=>ModelRequest.Build(fourInput,"fixture-model",_=>bytes),"more than four images rejected");
        var jury=ModelRequest.Build(input,"fixture-model",_=>bytes);
        Check(!jury.messages[0].content[0].text.Contains("One short evidence-based sentence.") &&
              !jury.messages[0].content[0].text.Contains("Return exactly {") &&
              jury.messages[0].content[0].text.Contains("status assessed"), "jury prompt states assessed status and contains no copyable answer template");
        string juryFacts=jury.messages[1].content[0].text;
        Check(new[]{"ORDER_SECRET","BRIEF_SECRET","SOURCE_SECRET","CUSTOMER_SECRET","CUSTOMER_ID_SECRET","SECRET_INDEX"}.All(secret=>!juryFacts.Contains(secret)),"jury request structurally excludes order, client and history context");
        input.order.budget=424242;input.order.deterministicRequirements.Add(new OrderRequirement{type="GAME_RULE_SECRET"});
        var context=ModelRequest.Build(input,"fixture-model",_=>bytes,EvaluationTask.Context);
        Check(context.messages[1].content[0].text.Contains("CUSTOMER_SECRET")&&!context.messages[1].content[0].text.Contains("houseId"),"context request receives preferences without jury votes");
        Check(!context.messages[1].content[0].text.Contains("GAME_RULE_SECRET")&&!context.messages[1].content[0].text.Contains("424242"),"semantic context cannot be confused with deterministic game thresholds or budget");
        string independentJury = DiagnosticJson.Serialize(ModelRequest.Build(input, "fixture-model", _ => bytes));
        input.order.brief.status = "runtime-history-verified"; input.order.brief.summary = "RUNTIME_BRIEF_SECRET";
        input.order.brief.sourceUrl = "game://history/0/quest/fixture";
        var runtimeContext = ModelRequest.Build(input, "fixture-model", _ => bytes, EvaluationTask.Context);
        Check(runtimeContext.response_format.type == "json_schema" && runtimeContext.response_format.json_schema.strict &&
            !DiagnosticJson.Serialize(ModelRequest.Build(input, "fixture-model", _ => bytes)).Contains("json_schema"),
            "context requests enforce structured decoding while baseline jury format remains unchanged");
        Check(runtimeContext.messages[1].content[0].text.Contains("RUNTIME_BRIEF_SECRET") &&
            runtimeContext.messages[0].content[0].text.Contains("runtime-history-verified"), "verified runtime card reaches the context request and its admission instruction");
        Check(DiagnosticJson.Serialize(ModelRequest.Build(input, "fixture-model", _ => bytes)) == independentJury,
            "runtime order brief does not alter independent jury evidence or instructions");
        JuryContract.ApplyContext(Context("consistent", 8), input, result);
        Check(result.orderCompliance.score == 8 && result.summary.average == 7, "runtime-card context validates without changing jury mean");
        input.order.brief.sourceUrl = null;
        Check(!JuryContract.HasOrderContext(input), "runtime status alone without source is insufficient context");
        input.order.definition = null;
        Check(EvaluationReadiness.Check(input,"fixture-model").canEvaluate && EvaluationReadiness.Check(input,"fixture-model").limitations.Contains("explicit-order-unavailable"), "unavailable order cannot block standalone visual jury");
        input.customer.status="unknown";input.order.brief.status="missing";
        Refuse(()=>ModelRequest.Build(input,"fixture-model",_=>bytes,EvaluationTask.Context),"no wasted context request when verified inputs absent");
    }
}
