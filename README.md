# Caption Translator

Aplicativo desktop WPF para Windows que captura uma região da tela, reconhece legendas em inglês com OCR local e traduz o texto para português usando um modelo Argos Translate local.

O projeto é um protótipo funcional. A captura, o OCR e a tradução são executados localmente. O aplicativo não envia imagens, legendas ou telemetria para a internet.

## O que o aplicativo faz

- Permite selecionar manualmente a região da tela onde aparecem as legendas.
- Captura essa região periodicamente usando GDI/`CopyFromScreen`.
- Reconhece inglês usando `Windows.Media.Ocr`.
- Aguarda texto estável por dois frames antes de exibi-lo.
- Acumula o transcript em inglês sem repetir trechos sobrepostos.
- Traduz cada segmento novo de legenda com o modelo neural Argos EN->PT e concatena o resultado sem repetir o transcript anterior.
- Mantém a tradução no segundo campo e evita repetir legendas idênticas.
- Executa o Argos em um serviço HTTP local em `127.0.0.1:8765`.
- Permite escolher entre OCR de tela e captura do áudio do sistema via loopback WASAPI.
- Transcreve o áudio localmente com Whisper antes de enviar o texto ao tradutor.

## Requisitos

### Sistema operacional

- Windows 10 versão 19041 ou superior; Windows 11 é recomendado.
- WSL2 instalado e habilitado.
- Uma distribuição Linux instalada no WSL2. Os comandos abaixo usam `Ubuntu`.
- Acesso à internet somente durante a instalação do SDK, Python, Argos e modelo. Depois da instalação, a tradução funciona offline.

### Ferramentas Windows

- .NET 8 SDK para Windows.
- .NET 8 Desktop Runtime, caso o aplicativo seja executado por `.exe` em um computador que não tenha o SDK.
- PowerShell 5.1 ou PowerShell 7.
- Dispositivo de saída de áudio do Windows habilitado para o modo de áudio.
- Microsoft Visual C++ Redistributable 2019 ou superior (x64) para o runtime CPU do Whisper.

### Ferramentas WSL

- Python 3.12.
- `curl`.
- `venv` ou a possibilidade de criar um ambiente virtual sem `sudo` usando o procedimento deste documento.

## Estrutura importante

```text
CaptionTranslator.sln
README.md
src/CaptionTranslator/
  CaptionTranslator.csproj
  MainWindow.xaml
  MainWindow.xaml.cs
  Capture/ScreenCaptureService.cs
  Ocr/WindowsOcrService.cs
  Pipeline/CaptionStabilizer.cs
  Pipeline/TranscriptBuffer.cs
  Translation/ArgosTranslator.cs
  Translation/OfflinePhraseTranslator.cs
tools/
  argos_service.py
tests/CaptionTranslator.Tests/
```

O caminho do repositório no WSL é usado pelo aplicativo para iniciar `tools/argos_service.py`. Em outro computador, configure `CAPTION_TRANSLATOR_WSL_PATH` conforme explicado abaixo.

## Instalação em outro computador

### 1. Instalar ou verificar o WSL2

Abra o PowerShell como administrador e execute:

```powershell
wsl --status
wsl --list --verbose
```

Se o WSL ainda não estiver instalado:

```powershell
wsl --install -d Ubuntu
```

Reinicie o Windows se solicitado e abra a distribuição Ubuntu pelo menu Iniciar uma vez para criar o usuário Linux.

Confirme o nome exato da distribuição:

```powershell
wsl --list --quiet
```

Se o nome não for `Ubuntu`, use o nome exibido ao configurar `CAPTION_TRANSLATOR_WSL_DISTRO`.

### 2. Obter o projeto

Coloque o projeto em uma pasta acessível pelo WSL. A forma mais simples é clonar ou copiar o repositório dentro do diretório Linux:

```bash
mkdir -p ~/workspace
cd ~/workspace
# clone ou copie o projeto para ~/workspace/caption-translator
cd ~/workspace/caption-translator
pwd
```

O comando `pwd` deve retornar o caminho que será usado em `CAPTION_TRANSLATOR_WSL_PATH`.

Também é possível manter o projeto em uma pasta do Windows, por exemplo `C:\dev\caption-translator`. Nesse caso, no WSL o caminho normalmente será `/mnt/c/dev/caption-translator`.

### 3. Verificar o Python no WSL

Dentro do Ubuntu:

```bash
python3.12 --version
curl --version
```

O Python precisa ser 3.9 ou superior. Python 3.12 é recomendado.

Se `python3.12` não existir e você tiver permissões administrativas:

```bash
sudo apt update
sudo apt install -y python3.12 python3.12-venv curl
```

Se não puder usar `sudo`, use um Python já disponível com versão 3.9 ou superior. O procedimento abaixo cria o ambiente com `--without-pip` e instala o `pip` diretamente, portanto não depende do pacote `python3.12-venv`.

### 4. Instalar o Argos Translate

Execute na raiz do projeto, dentro do WSL:

```bash
cd ~/workspace/caption-translator

rm -rf .argos-venv312
python3.12 -m venv --without-pip .argos-venv312
curl -fsSL https://bootstrap.pypa.io/get-pip.py | .argos-venv312/bin/python -
.argos-venv312/bin/pip install argostranslate
```

O pacote pode instalar várias dependências e ocupar bastante espaço. Isso é normal. O Argos usa o runtime de inferência CTranslate2 e inclui dependências opcionais nas versões atuais.

### 5. Baixar o modelo inglês-português

Ainda dentro da raiz do projeto:

```bash
.argos-venv312/bin/python - <<'PY'
import argostranslate.package

argostranslate.package.update_package_index()
package = next(
    package
    for package in argostranslate.package.get_available_packages()
    if package.from_code == "en" and package.to_code == "pt"
)
print(f"Baixando {package.from_code}->{package.to_code} v{package.package_version}")
argostranslate.package.install_from_path(package.download())
print("Modelo instalado")
PY
```

Teste o modelo sem iniciar o aplicativo:

```bash
.argos-venv312/bin/python - <<'PY'
import argostranslate.translate

examples = [
    "This is an old machine that they would use to make plywood.",
    "I love music and traveling. How about you?",
]
for example in examples:
    print("EN:", example)
    print("PT:", argostranslate.translate.translate(example, "en", "pt"))
PY
```

### 6. Configurar o caminho usado pelo aplicativo

O código tem um caminho padrão para este ambiente:

```text
/home/dev/workspace/caption-translator
```

Em outro computador, configure o caminho real do projeto no WSL antes de abrir o `.exe`.

Exemplo temporário para a sessão atual do PowerShell:

```powershell
$env:CAPTION_TRANSLATOR_WSL_PATH = "/home/seu-usuario/workspace/caption-translator"
$env:CAPTION_TRANSLATOR_WSL_DISTRO = "Ubuntu"
```

Se o projeto estiver no disco C:, por exemplo `C:\dev\caption-translator`, use:

```powershell
$env:CAPTION_TRANSLATOR_WSL_PATH = "/mnt/c/dev/caption-translator"
```

Para tornar permanente para o usuário do Windows:

```powershell
[Environment]::SetEnvironmentVariable(
  "CAPTION_TRANSLATOR_WSL_PATH",
  "/home/seu-usuario/workspace/caption-translator",
  "User"
)
[Environment]::SetEnvironmentVariable(
  "CAPTION_TRANSLATOR_WSL_DISTRO",
  "Ubuntu",
  "User"
)
```

Feche e reabra o PowerShell ou o atalho depois de alterar variáveis permanentes.

### 7. Instalar o .NET 8

No Windows, instale o **.NET 8 SDK** pelo instalador oficial da Microsoft. O SDK inclui o suporte de build para WPF.

Verifique pelo PowerShell:

```powershell
dotnet --info
dotnet --list-sdks
```

Para executar apenas o `.exe`, o **.NET 8 Desktop Runtime** também é suficiente. Para restaurar, testar e compilar, instale o SDK.

## Traducao por audio do sistema

Na janela principal, escolha **Audio do sistema** em **Fonte da traducao**. O aplicativo captura o dispositivo de saida padrao do Windows via WASAPI loopback; ele nao usa o microfone. O audio e convertido para texto pelo Whisper local e passa pelo mesmo fluxo de traducao do OCR.

Baixe um modelo Whisper em uma pasta local. O modelo pequeno em ingles e uma opcao inicial equilibrada entre latencia e qualidade:

```powershell
New-Item -ItemType Directory -Force .\models | Out-Null
Invoke-WebRequest `
  -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin?download=true" `
  -OutFile .\models\ggml-base.en.bin
```

Por padrao, o aplicativo procura `models\ggml-base.en.bin` ao lado do executavel. Para usar outro caminho:

```powershell
$env:CAPTION_TRANSLATOR_WHISPER_MODEL = "C:\modelos\ggml-base.en.bin"
```

O reconhecimento e executado localmente, mas modelos maiores aumentam o uso de CPU e a latencia. O primeiro teste deve ser feito com o modelo `base.en` e audio em ingles. Se o modelo nao existir ou o dispositivo de saida nao estiver disponivel, a janela exibira o erro e o modo nao sera iniciado.

## Compilar e testar

Abra o PowerShell na raiz do projeto ou use o caminho WSL equivalente. Execute:

```powershell
dotnet restore .\CaptionTranslator.sln
dotnet test .\tests\CaptionTranslator.Tests\CaptionTranslator.Tests.csproj
dotnet build .\src\CaptionTranslator\CaptionTranslator.csproj
```

Resultado esperado:

```text
Passed! - Failed: 0, Passed: 16
Build succeeded.
0 Warning(s)
0 Error(s)
```

## Executar

### Pelo SDK

```powershell
dotnet run --project .\src\CaptionTranslator\CaptionTranslator.csproj
```

### Pelo executável compilado

```powershell
Start-Process ".\src\CaptionTranslator\bin\Debug\net8.0-windows10.0.19041.0\CaptionTranslator.exe"
```

O serviço Argos é iniciado automaticamente pelo aplicativo através de `wsl.exe`. Não é necessário iniciar `tools/argos_service.py` manualmente.

### Atalho da área de trabalho

Crie um atalho apontando para:

```text
src\CaptionTranslator\bin\Debug\net8.0-windows10.0.19041.0\CaptionTranslator.exe
```

Se o executável for acessado pelo Windows através de um projeto dentro do WSL, o destino pode ser um caminho como:

```text
\\wsl.localhost\Ubuntu\home\seu-usuario\workspace\caption-translator\src\CaptionTranslator\bin\Debug\net8.0-windows10.0.19041.0\CaptionTranslator.exe
```

## Como usar

1. Abra o aplicativo.
2. Clique em **Selecionar area**.
3. Arraste sobre a região que contém as legendas em inglês.
4. Solte o mouse e confirme a seleção.
5. Clique em **Iniciar**.
6. Mantenha o vídeo, reunião ou navegador visível.
7. Aguarde o OCR estabilizar por alguns frames.
8. Confira o texto em **Legenda reconhecida (ingles)** e a tradução em **Traducao (portugues)**.
9. Clique em **Parar** antes de selecionar outra região ou iniciar uma nova sessão.

Ao clicar em **Iniciar**, os dois transcripts são limpos. O aplicativo não repete legendas idênticas e tenta mesclar legendas rolantes do YouTube.

## Arquitetura do fluxo

```text
GDI CopyFromScreen
        |
        v
Windows.Media.Ocr (en-US)
        |
CaptionStabilizer + TranscriptBuffer
        |
ArgosTranslator (.NET -> HTTP localhost)
        |
Argos service (WSL/Python/CTranslate2)
        |
Portuguese translation
```

O serviço local recebe somente JSON neste formato:

```json
{"text":"I love music and traveling."}
```

E responde:

```json
{"translatedText":"Adoro música e viagens."}
```

## Verificação manual do serviço Argos

Com o aplicativo aberto, confirme a porta local:

```bash
curl -fsS -X POST http://127.0.0.1:8765/translate \
  -H 'Content-Type: application/json' \
  -d '{"text":"This is a test."}'
```

Resposta esperada semelhante a:

```json
{"translatedText":"Isto é um teste."}
```

Se a porta não responder, execute manualmente no WSL:

```bash
cd /caminho/real/do/caption-translator
.argos-venv312/bin/python tools/argos_service.py
```

Depois execute o aplicativo novamente.

## Solução de problemas

### `wsl.exe` não encontrado

Instale o WSL2 no Windows e confirme com:

```powershell
wsl --status
wsl --list --quiet
```

### `The system cannot find the path specified`

O caminho configurado em `CAPTION_TRANSLATOR_WSL_PATH` não existe. Dentro do WSL, execute `pwd` na raiz do projeto e use exatamente esse resultado.

### `No module named argostranslate`

O ambiente virtual não foi criado ou o aplicativo está apontando para outra pasta. Confirme:

```bash
ls .argos-venv312/bin/python
.argos-venv312/bin/python -c "import argostranslate; print('ok')"
```

### O modelo EN->PT não foi encontrado

Reexecute a etapa de instalação do modelo. O pacote precisa ter `from_code == "en"` e `to_code == "pt"`.

### O serviço inicia, mas retorna erro 500

Teste o modelo diretamente:

```bash
.argos-venv312/bin/python -c \
  'import argostranslate.translate; print(argostranslate.translate.translate("Hello", "en", "pt"))'
```

Se esse comando falhar, reinstale o pacote de idioma. Se ele funcionar, verifique se `tools/argos_service.py` é o arquivo do mesmo projeto usado em `CAPTION_TRANSLATOR_WSL_PATH`.

### O OCR não reconhece corretamente

- Selecione a região inteira da legenda.
- Evite incluir muita área vazia ou outros textos.
- Use fonte com bom contraste.
- Evite selecionar uma região cortando a primeira ou última linha.
- Verifique se o pacote de reconhecimento de fala/idioma inglês do Windows está disponível.

### A janela abre, mas a tradução demora

O primeiro uso pode carregar o modelo na memória. O modelo Argos é local e pode consumir CPU/RAM. Aguarde alguns segundos antes de concluir que falhou.

### O projeto compila, mas o `.exe` pede Desktop Runtime

Instale o **.NET 8 Desktop Runtime x64** no Windows. O SDK não precisa ser instalado para usuários que apenas executam um `.exe`, mas é necessário para desenvolvimento e build.

## Limitações conhecidas

- A aplicação foi projetada inicialmente para o monitor principal.
- Escala de DPI, múltiplos monitores e coordenadas especiais ainda precisam de refinamento.
- A qualidade depende do OCR e do contexto que aparece na região selecionada.
- O modelo Argos é significativamente melhor que o dicionário local, mas não equivale a serviços comerciais como DeepL ou Google Translate.
- O serviço local é iniciado por `wsl.exe`; portanto, o WSL precisa estar disponível para a tradução.
- O modo atual não cria ainda uma janela de legenda sobreposta ao vídeo.

## Privacidade

O OCR, a captura e a tradução são locais. O aplicativo não usa MyMemory, Google, DeepL ou outro serviço externo. As imagens capturadas permanecem em memória e não são gravadas em arquivo. As legendas não são persistidas depois que o aplicativo é fechado.

## Licenças e atribuições

O código do projeto deve seguir a licença definida pelo repositório quando ela for adicionada. O modelo Argos e seus pacotes de idioma possuem licenças e termos próprios; verifique os arquivos de licença distribuídos pelo pacote antes de redistribuir o modelo junto com um instalador comercial.
