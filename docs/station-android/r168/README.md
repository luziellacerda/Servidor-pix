# R168 recebida; cadastro ainda não aplicado

O kit privado e a cadeia de recibos do APK final foram verificados. O preflight
conjunto foi recusado pelo orçamento de perfis de WinBack:137.167/131.072B já
nos primeiros78 vínculos. Nenhum registro R168 foi publicado. A R167 segue
registrada e o servidor está online.

Ler [retorno consolidado](server-return.json) e o topo do
[mesmo handoff](../HANDOFF-ONLINE-PLATAFORMAS-STATION-20261009.md).
A requalificação PSX isolada passou, incluindo as negativas de HLE/core/edição;
isso não representa cadastro, capabilities assinadas ou gameplay R168.
Não repetir cadastros anteriores nem pedir o kit novamente. Resolver o
orçamento com preservação dos históricos e do contrato antes de uma nova
baseline, validação completa e publicação transacional.

## Origem do handoff de produção, preservada

# R168 PSX Android XML — entrega documental ao servidor

Leia o [handoff R168](HANDOFF-R168-PSX-ANDROID-XML-20261015.md) e o [recibo do kit](server-kit-receipt-r168.json). Esta entrega é **documentação**, ainda com `serverApplied=false`. O repositório do servidor continua na R167 aplicada até haver cadastro aditivo e recibo efetivo da R168.

O [kit privado exato](https://github.com/luziellacerda/TurboramaEmulationStation/tree/4733c00fbba4be0c8dd5b9c813ba74002732d1b5/station-server-handoffs/r168), commit `4733c00fbba4be0c8dd5b9c813ba74002732d1b5`, contém o ZIP sem APK, SHA-256 `c0de71c3a4e10cce320f7ba6966e183871760157ca67e9010eb5bdfb0850177b`. A [fonte e receita do APK](https://github.com/luziellacerda/TurboElden/commit/e8c0e8c6cbeb57202c9193cbe4f3cd394da027ca) estão no TurboElden, ramo `build/station-r168-psx-xml-20261015`, commit `e8c0e8c6cbeb57202c9193cbe4f3cd394da027ca`.

O APK assinado R168 SHA-256 `875fb0696c4fb50ce4af953bccd62fcd68dfdecb247f65d2fc027b71aa330b3d` permanece fora do Git. Frente à R167, foram alteradas apenas `classes35.dex`, o manifesto dos motores e três marcadores vinculados. Os outros 39 DEX, carrossel, núcleos, opções e lógica N64 foram preservados. O defeito corrigido é a exceção XML do Android antes do anexo ao motor PSX. Os quatro IDs mudaram como consequência do vínculo ao APK final.

O servidor deve conferir o kit sobre a baseline **R167 já aplicada**, fazer cadastro aditivo com proteção e requalificar modos por edição e tupla exata. Não repetir o operador R167, não aprovar por nome, não reiniciar ou alterar o serviço N64 por causa deste handoff. Depois do recibo aplicado e das capacidades públicas autenticadas, o produtor poderá atualizar manualmente os telefones e repetir a partida PSX real. Sala `playing`, QEMU e teste sintético não comprovam imagem, áudio e controles Android.
