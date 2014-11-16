namespace Imperium

open System
open System.IO
open System.Text

module IOUtils =
    let toString (encoding:Encoding) (is: Stream) =
        let arr = Array.zeroCreate 1024
        let rec loop (acc:StringBuilder) =
            match is.Read(arr, 0, arr.Length) with
            | -1 | 0 -> acc
            | _ -> acc.Append(encoding.GetString arr) |> loop
        (loop (new StringBuilder())).ToString() 