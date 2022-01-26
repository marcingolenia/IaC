open Pulumi
open pulumi_iac.MyStack

[<EntryPoint>]
let main args =
    Deployment.RunAsync<MyStack>().Result |> ignore
    0