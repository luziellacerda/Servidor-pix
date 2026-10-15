# Diagnóstico com o core ARM64 recebido

`headless_probe.cpp` é o frontend diagnóstico usado nesta rodada. Não substitui
o LibretroPlayer, não é incorporado ao APK e não valida seus hooks de execução.
O ELF de core usado é o original privado, SHA
`e7534aa6f4a993e03f4563152f0e017324b710f5a5c224b0f1f722abeabed2b9`.

## Ambiente reproduzível

- Android NDK 27.1.12297006, clang `aarch64-linux-android28-clang++`.
- QEMU user-mode ARM64 8.2.2, pacote Ubuntu `1:8.2.2+ds-0ubuntu1.18`.
- Bionic/linker ARM64 originais do [prebuilt AOSP fixado](https://android.googlesource.com/platform/prebuilts/runtime/+/070571b455076f77a01c7b07154a15e545d2b428/mainline/runtime/apex/com.android.runtime-arm64.apex).
  APEX SHA `83bf0dce249728dae48149b80d28b48115c54adad95a352120d58a6ac669d1fc`.
- zlib 1.3.1 compilada para o mesmo ABI; adaptador mínimo de log Android para stderr.
- `libretro.h` do [upstream exato](https://github.com/notaz/pcsx_rearmed/tree/ff81ed17a15241d2f3730cdd7585b38e172532ca).
- `PsxPinnedOptions.h` exato de `integration/psx-online/native/` no TurboElden,
  política SHA `8c1e81460dc19b6e8d151ec6231c9092000b4946e6f084222cb40d3aab2e66a9`.

Compilar C++20 com `-O2 -fPIE -pie -static-libstdc++ -ldl`, informando ao linker
o caminho privado do `linker64` extraído. Definir `LD_LIBRARY_PATH` somente no
processo diagnóstico para Bionic, zlib e o adaptador de log. Não instalar esses
arquivos sobre bibliotecas do host. Executar `nice -n 15`, com afinidade de duas
CPUs, `qemu-aarch64 frontend core.so conteudo diretorio-privado-novo`.

Cada instância recebe diretório novo sem BIOS/cards existentes. O log precisa
confirmar HLE e nenhuma opção `UNPINNED_OPTION`. Os dois ports são Standard;
`GET_INPUT_BITMASKS` retorna falso, como no owner real que usa IDs 0 a 13.

## Método por edição

1. Conferir o conteúdo contra o catálogo imutável. Para CUE, usar o CUE efetivo
   publicado e todas as faixas no hash canônico `cue-set-v1`; não o CUE original
   com referências Windows ou nomes ausentes. Não modificar ROMs.
2. Abrir duas instâncias independentes do mesmo ELF. Transferir o estado inicial
   antes do primeiro quadro; conferir retorno de `retro_unserialize` e RAM.
3. Rodar entradas idênticas nos dois processos, comparando RAM e amostras de
   vídeo. Os 31 candidatos MP usam 2.400 quadros de boot + 360 de entradas
   distintas entre os dois ports; os demais usam 600 + 360. Registrar o serial
   efetivamente lido pelo core.
4. Serializar o anfitrião nesse cursor, restaurar no segundo processo e rodar
   outros seis blocos de 30 quadros. Conferir RAM e vídeo, preservando falhas.
5. Inspecionar visualmente os checkpoints. Uma tela preta isolada pode ser uma
   transição: Digimon, FFIV, FFIX e MK3 foram continuados e produziram telas
   significativas. Essa observação não prova todos os níveis de um jogo.
6. Encerrar as instâncias e conferir que inode/tamanho/mtime das fontes não
   mudaram. Estados e imagens permanecem privados; publicar só hashes/achados.

Comandos de stdin: `step quadros mask1 mask2`, `status`, `save caminho`,
`load caminho`, `memory caminho`, `image caminho`, `quit`. Há limite de 10.000
quadros por comando e máscaras de 14 bits. Cada resposta reflete chamadas reais
ao core. O estado observado tem 4.456.448 bytes e a RAM principal 2.097.152 bytes.

O resultado compara dois processos diagnósticos. Não mede atraso WAN nem executa
o aplicativo Android. A prova de transporte usa seus próprios clientes sintéticos
e está descrita separadamente nos recibos; tampouco equivale a gameplay físico.
