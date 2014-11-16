namespace Imperium

open System
open System.IO 
open System.Collections 
open System.Collections.Concurrent

module Actor =
    type 'a AtomicRef(v: 'a) =
        let sync = new Object()
        let mutable refValue = v

        member this.Value
            with get() = lock sync (fun () -> refValue)
            and set(value) = lock sync (fun () -> refValue <- value)

        member this.Sync f =
            lock sync (fun () -> f refValue)

    type 'a Mailbox (receive: 'a -> unit) =
        let queue = new ConcurrentQueue<obj>()

        let running = AtomicRef(false)

        member this.Kill() =
            running.Value <- false

        member this.Start() =
            running.Value <- true
            async {
                while running.Sync (fun running ->
                    if not running then false
                    else 
                        let mutable msg: obj = new Object()
                        if queue.TryDequeue(&msg) then
                            receive (msg :?> 'a)
                            true
                        else true
                ) do ()
                return ()
            } |> Async.Start

        member this.Post (msg: 'a) =
            queue.Enqueue(msg)
            
