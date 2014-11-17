
open System
open System.IO
open System.Diagnostics

open Imperium
open Imperium.Actor

open NLog

type Log =
    | Info of string
    | Debug of string
    | Error of string * exn
    | Callback of (unit -> unit)

type Logger(name:string) =
    //let fileStream = File.OpenWrite("G:\out.log")
    //let writer = new StreamWriter(fileStream)
    let mutable lines = 0

    let format = sprintf "%s | %A | %s | %s"

    let formatType =
        let date = DateTime.Now
        function
        | Info msg -> format "INFO" date name msg
        | Debug msg -> format "DEBUG" date name msg
        | Error (msg, e) -> format "ERROR" date name msg
        | Callback f -> format "INFO" date name ""

    let target = "G:\out.log"
    let write msg =
        File.AppendAllLines(target, [ msg ])

    let log msg =
        formatType msg 
        |> write

    let agent = Mailbox<Log>(fun msg ->
        log msg

        lines <- lines + 1

        (*if lines % 1000 = 0 then
            fileStream.Flush()*)
    )

//    let agent = new MailboxProcessor<Log>(fun inbox ->
//        let rec loop() = async {
//            let! msg = inbox.Receive()
//
//            match msg with
//            | Info msg -> msg
//            | Debug msg -> msg
//            | Error (msg, e) -> msg
//            | Callback f -> f(); "Done"
//            |> writer.WriteLine
//
//            lines <- lines + 1
//
//            if lines % 10000 = 0 then
//                fileStream.Flush(true)
//
//            return! loop()
//        }
//        loop()
//    )

    member this.Lines = lines

    member this.Start() =
        agent.Start()

    member this.Info msg =
        agent.Post (Info msg)

    member this.Callback f =
        agent.Post (Callback f)

    interface IDisposable with
        member this.Dispose() = 
            ()
            (*writer.Close()
            fileStream.Close()*)

[<EntryPoint>]
let main argv = 
    //let server = Server.Server("http://localhost:8080/")
    //server.Start()

    let watch = new Stopwatch()

    let tasks = 1000
    let max = 10

    use log = new Logger("Logger")

    let logger = LogManager.GetLogger("Main")
    
    log.Start()
    watch.Start()

    [ for i in 0..tasks-1 do yield async {
        for j in 0..max do 
            //logger.Info("Hello")
            log.Info "Hello"
    } ] |> Async.Parallel 
    |> Async.RunSynchronously 
    |> ignore
    
    printfn "%A" watch.Elapsed |> ignore

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
