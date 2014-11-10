namespace Imperium

open System
open System.IO
open System.Text
open System.Net
open Newtonsoft.Json

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


    type Credentials =
        {   Name: string;
            Password: string;
        }

    type ClientProfile = 
        {   Name: string;
            Password: string;
            Storage: ClientStorage;
        }

    type ClientInfo = 
        {   FirstName: string;
            LastName: string;
            UserName: string;
            UserPassword: string;
        }

    type InboundMsg =
        | Login of Credentials
        | Register of ClientInfo

    type HttpHandler = HttpListenerRequest->HttpListenerResponse->Async<unit>

    type Server(host: string) =
        let encoding = new UTF8Encoding()

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

        let handleGET: HttpHandler = fun req resp ->
            let root = "/os"
            match req.Url.AbsolutePath with
            | path when path = root+"/login" ->
                
            
        member this.Start() =
            listener (fun req resp ->
                async {
                    try 
                        match req.HttpMethod with 
                        | "GET" -> handleGet req resp
                        | "POST" -> handlePost req resp
                        | method -> 
                            resp.StatusCode <- 400
                            resp.Close()
                })
