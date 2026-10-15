# R161 — falha física de vídeo N64 online (15/10/2026)

## Resultado observado

No moto g52, a partida online de **007: The World Is Not Enough** com outro telefone passou pelo registro do netplay e chegou ao estado PLAYING. Os controles e o áudio responderam, mas a tela permaneceu preta e o áudio ficou lento. A superfície do jogo apresentou apenas o quadro inicial; a interface Android continuou desenhando.

Trecho mínimo do registro privado do aparelho (segundos relativos ao início do jogo):

```text
CoreService: Netplay is ready!
CoreInterface: Using plugin for type: M64PLUGIN_GFX:mupen64plus-video-glide64mk2
Core: Setting video mode: 640x480
Core: SDL_SetVideoMode failed: (null)
mupen64plus-video-glide64mk2: Error setting display mode
StationN64: event=state state=2
Core: Starting R4300 emulator: Dynamic Recompiler
```

Após a saída manual da partida, abri **a mesma edição de 007 no mesmo moto g52, com o mesmo APK R161 e o mesmo plugin Glide64mk2**, pelo botão de jogar sozinho. A imagem apareceu normalmente e não houve `SDL_SetVideoMode failed` nesse início. Logo, trocar o plugin gráfico ou alterar o servidor de retransmissão não é a correção comprovada para esta falha.

## Causa no caminho de inicialização

A [ponte oficial do Mupen64Plus AE](https://github.com/mupen64plus-ae/mupen64plus-ae/blob/33bf7021549d7873e976884370fbd1a8e7333bc7/ae-bridge/src/ae_bridge.cpp) implementa `overrideAeVidExtFuncs()` com `dlopen("libmupen64plus-core.so")`, seguido de `CoreOverrideVidExt(&vidExtFunctions)`. A ponte integrada no APK R161 mantém esse nome fixo. Já o N64 online escolhe `libstation_mupen64plus_core.so` em `StationN64Bridge.coreName()`. Portanto a tabela de vídeo Android é instalada no core offline, enquanto o core online fica sem a extensão.

No [código oficial do core](https://github.com/mupen64plus-ae/mupen64plus-ae/blob/33bf7021549d7873e976884370fbd1a8e7333bc7/mupen64plus-core/upstream/src/api/vidext.c), `VidExt_SetVideoMode` usa a função da extensão quando ela está ativa. Sem a extensão, cai em `SDL_SetVideoMode`, exatamente o caminho e a falha registrados no g52. O mesmo plugin funcionando offline reforça que o problema é a associação da extensão com a instância errada do core.

## Correção delimitada para a próxima candidata

Compilar uma ponte de vídeo separada para a sessão online com `dlopen("libstation_mupen64plus_core.so")`, preservando a biblioteca `libae-bridge.so` original para o modo offline. `CoreInterface` deve carregar a ponte correspondente ao core da sessão tanto em `System.loadLibrary` quanto em `Native.load`, antes de `CoreStartup`/`CoreOverrideVidExt`. A compilação e o empacotamento devem comprovar o nome da biblioteca, o símbolo, a instalação da extensão no core online e a preservação dos arquivos offline.

**Estado:** R161 não foi homologada para N64 online. Ainda falta instalar uma candidata corrigida nos dois telefones, receber seu cadastro exato no servidor e observar imagem, fluidez, controle e saída numa partida física. O convidado permaneceu na tela preta quando o anfitrião saiu, sem aviso imediato; a propagação do encerramento deve ser validada separadamente com registros do convidado.

As capturas completas, screenshot e registros do aparelho ficam em entrega privada fora do Git. Não contêm material para publicação no repositório.
