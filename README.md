[![.NET](https://github.com/adrian-bulicanu/tailp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/adrian-bulicanu/tailp/actions/workflows/dotnet.yml) [![Build Status](https://dev.azure.com/adrianbulicanu/tailp/_apis/build/status/adrian-bulicanu.tailp?branchName=master)](https://dev.azure.com/adrianbulicanu/tailp/_build/latest?definitionId=1&branchName=master)

# tailp

tailp (tail+) - Outputs, filters and highlights text from file(s) on disk, network and archives.
Inspired by tail and grep command line utilities.

See the [help.txt](tailp/Resources/help.txt) file for details.

## Example

`tailp -t c:\windows\*.log -n 30 -f -S COMAPI -S START -R -L "ClientId = .*" -nr -N`

![Screenshot](docs/sample_screenshot.png)

## Prerequisites

.NET 5

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
