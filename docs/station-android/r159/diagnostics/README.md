# Captura privada do N64 na produção

Ferramenta de diagnóstico, sem instalação de APK. O cadastro R159 já está aplicado.
A tela preta de 007 com áudio travando ainda não tem causa confirmada.
O identificador do pacote consultado no Android é `org.turboramastation.frontend`.

## Executar no PC conectado ao telefone

Requer Python 3.8+ e ADB autorizado. Abra o Station; espere a mensagem
“Captura iniciada” antes de reproduzir uma tentativa nova.

```sh
python capture_n64_online.py --role host --duration 120 --debug-tags
python capture_n64_online.py --role guest --duration 120 --debug-tags
```

Use a primeira linha no anfitrião e a segunda no convidado, durante a mesma
partida. Com dois telefones no mesmo PC, execute em dois terminais, acrescentando
`--serial SERIAL_LOCAL` com o identificador correspondente do `adb devices`.
Se ADB não estiver no PATH, acrescente `--adb CAMINHO_DO_ADB`. No Windows,
`py -3` pode substituir `python`; no Linux, `python3`.

A pasta de saída aparece no terminal: `StationDiagnostics` no diretório do
usuário. É possível escolher a base com `--output PASTA_PRIVADA`.
Mantenha o USB conectado até terminar. Anote jogo/edição, modo, papéis dos dois
aparelhos, horário da falha e identificação do APK já conferida pela produção.
Não calcule hashes de APK/ROM durante a tentativa. A versão do pacote coletada
pelo script não comprova sozinha o hash do APK instalado.

## Conteúdo e efeitos

- Logs novos de tags selecionadas, com horário epoch, sem apagar os buffers.
- Histórico de encerramentos, versão do pacote, modelo e informações de GLES.
- PID principal e `:n64core`, CoreService, janela/diálogo, SurfaceFlinger e CPU do core.
- Instantâneos a cada aproximadamente 20 segundos, dentro da duração escolhida.
- Sem `--debug-tags`, apenas consultas. Com a opção, cinco propriedades
  temporárias `log.tag.*` são salvas, ativadas e restauradas/verificadas ao sair.
  Não são propriedades persistentes. Se o cabo cair ou o processo for encerrado
  à força, os valores originais ficam em `original-log-properties.json` para a
  produção restaurar no mesmo telefone; não declarar restauração sem conferência.
- O app continua aberto: o único processo encerrado pelo script é seu próprio
  leitor ADB no PC. Não há instalação, limpeza de dados, leitura de ROM/APK,
  `adb root`, captura de tela ou coleta da hierarquia da interface.

`capture-manifest.json` distingue captura concluída de interrupção. Se o leitor
ADB encerrar cedo, a captura falha e preserva os dados parciais. Permissões
negadas em consultas ficam registradas; ausência de dados não prova ausência de
problema. A fixture cobre término normal, restauração das tags e leitor encerrado
cedo. Ela não usou Android físico.

## Cruzamento antes de escolher uma correção

Conferir o mesmo intervalo UTC do servidor, o mesmo APK/motor e a mesma edição.
Relacionar abertura do core, roster/READY e eventos de recuperação existentes nos
logs aos bytes/epoch/pausas da sala. Relacionar erros EGL/Surface e estado da janela
à CPU e aos intervalos sem apresentação. A lista/latência do SurfaceFlinger não
mostra o conteúdo desenhado nem comprova que o jogo iniciou.

`gfxinfo` mede a interface; não é FPS/VI do emulador. O script também não acrescenta
contadores nativos ausentes no build. Se os logs não contiverem VI e duração das
esperas de input, registrar essa limitação antes de concluir entre custo local e
espera de rede. Histórico de encerramentos exige correlação de horário; um erro
antigo não é evidência desta tentativa.

Referências: [logcat oficial](https://developer.android.com/tools/logcat) e
[limites da medição de renderização Android](https://developer.android.com/topic/performance/issues/render).

Enviar a pasta por entrega privada em `SERVIDOR LER`; não fazer commit dos logs.
A pasta tem `.gitignore` com `*` e permissões restritas em POSIX; no Windows,
permanece sujeita às permissões da conta. O manifesto sempre mantém
`physicalGameplayValidated=false`: concluir a coleta não aprova uma partida.
