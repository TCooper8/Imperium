
open System
open System.IO
open System.Diagnostics

open Imperium
open Imperium.Actor

type Log =
    | Info of string
    | Debug of string
    | Error of string * exn
    | Callback of (unit -> unit)

type Logger() =
    let fileStream = File.OpenWrite("out.txt")
    let writer = new StreamWriter(fileStream)
    let mutable lines = 0

    (*let agent = Mailbox<Log>(fun msg ->
        match msg with
        | Info msg -> msg
        | Debug msg -> msg
        | Error (msg, e) -> msg
        | Callback f -> f(); "Done"
        |> writer.WriteLine

        lines <- lines + 1

        if lines % 10000 = 0 then
            fileStream.Flush(true)
    )*)

    let agent = new MailboxProcessor<Log>(fun inbox ->
        let rec loop() = async {
            let! msg = inbox.Receive()

            match msg with
            | Info msg -> msg
            | Debug msg -> msg
            | Error (msg, e) -> msg
            | Callback f -> f(); "Done"
            |> writer.WriteLine

            lines <- lines + 1

            if lines % 10000 = 0 then
                fileStream.Flush(true)

            return! loop()
        }
        loop()
    )

    member this.Lines = lines

    member this.Start() =
        agent.Start()

    member this.Info msg =
        agent.Post (Info msg)

    member this.Callback f =
        agent.Post (Callback f)

    interface IDisposable with
        member this.Dispose() = 
            writer.Close()
            fileStream.Close()

[<EntryPoint>]
let main argv = 
    //let server = Server.Server("http://localhost:8080/")
    //server.Start()

    let watch = new Stopwatch()

    let tasks = 20
    let max = 1000000

    use log = new Logger()
    
    log.Start()
    watch.Start()

    [ for i in 0..tasks-1 do yield async {
        for j in 0..max do 
            log.Info "Hello"
    } ] |> Async.Parallel 
    |> Async.RunSynchronously 
    |> ignore

    log.Callback (fun () ->
        watch.Stop()

        let elapsed = watch.Elapsed
        let seconds = elapsed.TotalSeconds
        let lines = log.Lines

        printfn "%A" elapsed |> ignore
        printfn "%i lines writen" lines |> ignore
        printfn "%f lines per second" (float lines / elapsed.TotalSeconds)
    )

    Console.ReadLine() |> ignore
    0 
