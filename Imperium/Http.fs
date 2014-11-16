namespace Imperium 

open System.IO
open System.Text
open System.Net

module Http =
    type HttpResponse = 
        {   Encoding: Encoding;
            Status: int;
            OutputStream: Stream;
            Headers: Map<string, string list> 
        }

        
        ///<summary>
        ///  Documenation!
        ///</summary>
        static member Zero() =
            {   Encoding = new UTF8Encoding();
                Status = 200;
                OutputStream = new MemoryStream();
                Headers = Map<string, string list> []
            }

        member this.AddHeader (name, value) =
            match this.Headers.TryFind name with
            | None -> { this with Headers = this.Headers.Add(name, [ value ]) }
            | Some ls -> { this with Headers = this.Headers.Add(name, value::ls) }

    let respond fs (resp:HttpResponse) (listenerResp:HttpListenerResponse) =
        let res = List.fold (fun acc f -> f acc) resp fs
        listenerResp.ContentEncoding <- res.Encoding 
        listenerResp.StatusCode <- res.Status

        res.Headers 
        |> Map.iter (fun key values ->
            List.iter (fun value ->
                listenerResp.AddHeader(key, value)
            ) values
        )
        res.OutputStream.CopyTo(listenerResp.OutputStream)

    let respFunction f (resp:HttpResponse) =
        f resp

    let headerName name value =
        respFunction <| fun resp -> resp.AddHeader(name, value)

    let allow = headerName "Allow"
    let contentEncoding = headerName "Content-Encoding"
    let contentLength = headerName "Content-Length"
    let date = headerName "Age"

    let responseStream (stream:Stream) =
        respFunction <| fun resp ->
            try stream.CopyTo(resp.OutputStream)
            finally stream.Close()
            resp

    let responseBytes (data: byte[]) =
        let stream = new MemoryStream(data)
        responseStream stream

    let responseString (data:string) =
        let stream = new MemoryStream()
        let writer = new StreamWriter(stream)
        writer.Write(data)
        responseStream stream


