namespace Imperium

open System
open System.IO
open System.Text
open System.Net
open System.Collections.Generic
open Newtonsoft.Json

open Http

module Server =
    type DriveStorage =
        {   Drive: string;
            Root: string;
        }

    type RemoteStorage =
        {   EndPoint: Uri;
            Drive: DriveStorage;
        }

    type ClientStorage =
        | DriveStorage of DriveStorage
        | RemoteStorage of RemoteStorage
        
    type Username = string
    type Password = string

    type Credentials =
        {   Username: Username;
            Password: string;
        }

    type ClientProfile = 
        {   FirstName: string;
            LastName: string;
            Password: string;
            UserName: Username;
            Storage: ClientStorage;
        }

    type RegisterInfo =
        {   FirstName: string;
            LastName: string;
            Password: string;
        }

    type Uid = uint64

    type ClientSession = 
        {   Uid: Uid;
            Client: ClientProfile;
        }

    type InboundMsg =
        | Login of Credentials
        | Register of ClientProfile
        | InboundError of exn

    type OutboundMsg =
        | AcceptRegister 
        | RejectRegister of exn
        | AcceptLogin of Uid
        | RejectLogin of exn
        | OutboundError of exn

    type SessionManager(maxSessions: int) = 
        let mutable table = Map<Uid, ClientSession> []
        let mutable lruQueue = new LinkedList<Uid>()
        let mutable lastUid: Uid = 0UL

        let checkCap() =
            if table.Count >= maxSessions then
                let head = lruQueue.First
                table <- table.Remove(head.Value)
                lruQueue.RemoveFirst()
            else ()

        member this.Add (profile:ClientProfile) =
            match Map.tryFindKey (fun k v -> v.Client.UserName = profile.UserName) table with
            | Some uid -> uid
            | None ->
                lastUid <- lastUid + 1UL
                checkCap()
                table <- table.Add(lastUid, { Uid = lastUid; Client = profile })
                lruQueue.AddLast(lastUid) |> ignore
                lastUid

        member this.Remove uid =
            table <- table.Remove(uid)
            lruQueue.Remove(uid) |> ignore

        member this.TryFind uid =
            let res = table.TryFind uid
            if res.IsSome then
                lruQueue.Remove(uid) |> ignore
                lruQueue.AddLast(uid) |> ignore
            res

    type ProfileManager() =
        let mutable profiles = Map<Username, ClientProfile> []

        member this.TryFind username =
            profiles.TryFind username

        member this.Add profile =
            profiles <- profiles.Add (profile.UserName, profile)

    type HttpHandler = HttpListenerRequest->HttpListenerResponse->Async<unit>

    type Server(host: string) =
        let encoding = new UTF8Encoding()
        let s = sprintf

        let mutable sessions = SessionManager(100)
        let profileManager = ProfileManager()

        let listener (handler:HttpHandler) =
            let hl = new HttpListener()
            hl.Prefixes.Add host
            hl.Start()
            let task = Async.FromBeginEnd(hl.BeginGetContext, hl.EndGetContext)
            async {
                while true do
                    let! context = task
                    Async.Start(handler context.Request context.Response)
            } |> Async.Start
            

        let handleLogin (credentials:Credentials) =
            if Object.ReferenceEquals(credentials, null) then
                new Exception("Invalid credentials") |> RejectLogin
            else 
                match profileManager.TryFind (credentials.Username) with
                | Some profile ->
                    if credentials.Password = profile.Password then
                        sessions.Add profile |> AcceptLogin
                    else 
                        new Exception("Invalid password") |> RejectLogin
                | None -> new Exception("Invalid credentials") |> RejectLogin

        let handleRegister profile =
            profileManager.Add profile

        let handleInbound msg =
            match msg with
            | Login credentials -> 
                handleLogin credentials

            | Register profile -> 
                handleRegister profile
                AcceptRegister

            | InboundError e -> OutboundError e

        let handleGet: HttpHandler = fun req resp ->
            async {
                try 
                    let root = "/os"

                    let mkResponse f =
                        f (HttpResponse.Zero()) resp

                    match req.Url.AbsolutePath with
                    | path when path = root+"/login" ->
                        JsonConvert.DeserializeObject<Credentials>(
                            IOUtils.toString encoding (req.InputStream)
                        ) |> Login
                    | path -> InboundError (new Exception(s "Unable to match path of %s" path))

                    |> handleInbound
                    |> sprintf "%A" 
                    |> encoding.GetBytes
                    |> fun data ->
                        [   contentLength (data.Length.ToString());
                            responseBytes data;
                        ]
                    |> respond 
                    |> mkResponse
                with 
                    | e -> 
                        printfn "%A" e 
                        resp.Close()
            }
            
        member this.Start() =
            listener (fun req resp ->
                async {
                    try 
                        match req.HttpMethod with 
                        | "GET" -> return! handleGet req resp
                        | "POST" -> () // handlePost req resp
                        | httpMethod -> 
                            resp.StatusCode <- 400
                            resp.Close()
                    with 
                        | e -> 
                            resp.Close()
                            printfn "%A" e |> ignore
                })
