## Estado atual do servidor — 14/10/2026, conferido às 15h19 UTC

**Kit R149 recebido, validado e aplicado; N64 liberado para a produção testar até quatro jogadores nos modos revisados.** `serverRegistrationConfirmed=true`, `serverKitApplied=true`, `serverConsumerReady=true`, `n64ServerReady=true`, `n64ServerOffline=false`. [Recibo aplicado do kit](server-return.json), [prova pública N64 atual](evidence/server-n64-live-ready-20261014.json), [correção Wind Waker aplicada](evidence/server-wind-waker-applied-20261014.json).

A prova atual validou os 66 modos/33 edições, quatro slots, seis conexões autenticadas, barreira `playing`, troca isolada de dados e recuperação de queda. 26 edições aceitam quatro pessoas. O motor exato é `mupen64plus-ae-33bf7021-n64-v3-dcad075758e5`; o app deve selecionar seu perfil pela resposta assinada de `capabilities` e respeitar `allowedPlayerCounts`. Os outros 124 itens N64 não têm perfil revisado desse owner, e não devem receber aprovação inventada. Catálogo atual 49; 43 motores/11.682 perfis/6.821 modos.

Os indicadores antigos de kit pendente abaixo são históricos. `consumerReady`/homologação física do APK continuam separados da prontidão **do servidor**, agora confirmada. Nenhuma partida em quatro Android foi comprovada por esta prova de transporte. Não promover a R143 estável, trocar o APK ou reaplicar kit/operadores por esta atualização documental.

## Histórico da entrega do app preservado integralmente

# Recebimento do kit privado R149

**Pacote preparado; ainda não aplicado.** A fonte da receita é
`ef5d00b189a4a57bb2044e297b2f1b9b4fa90ce0` e a base assinada R148 tem
SHA-256 `c8b8c30cac7d36ec2a8ca8d470542096919435c749dede535cd0d58648e00dc1`.
O APK assinado R149 tem **2.715.089.935 bytes**, SHA-256
`698e8a7914cc7be2facc00e6701ff1f3533fa13f763358b51578a35d25c2741e`.
Receber somente `Kit-Servidor-TurboStations-R149-Independentes.zip`, de
**12.181.272 bytes**, SHA-256
`ed95ba61c92285937014a91e86b1dd7dd0a6928e10a9d063132973244e4eb5ec`.
Os hashes e tamanhos foram conferidos no backup privado local. A cópia na mídia
de repasse teve hash conferido anteriormente; a mídia não estava acessível
para nova leitura nesta atualização. O servidor ainda precisa receber/verificar
seus próprios bytes.
O kit R149 **não tem recibo de cadastro aplicado**; o kit R148 segue sem
aplicação comprovada e não substitui a R149.

O APK R149 foi instalado no Samsung A56 com SHA-256 conferido, sem desinstalar
ou limpar dados. O teste **offline** abriu o Dolphin sem repetir o antigo
`LinkageError`, mas revelou leitura fora da imagem de *Wind Waker* cadastrada.
Esse item requer quarentena e verificação do artefato conforme
[diagnóstico de mídia](ERRO-LEITURA-WIND-WAKER-20261014.md); não atribuir esta
falha ao kit nem declarar gameplay homologado. O teste **online** exige o kit/IDs
correspondentes ao APK R149 instalado. Quando o pacote final chegar:

1. Autenticar localmente e obter baseline protegido **atual** de catálogo,
   perfis, modos e motores antes de escrever; preservar retorno/rollback.
2. Conferir SHA/tamanho do ZIP e manifesto (38 arquivos, sendo 37 listados),
   os seis ELF e vinte dependências reais, os quatro IDs exatos abaixo e o
   ledger revisado de 78 modos
   (66 N64, nove GameCube, três Wii). Nenhuma aprovação PS1 HLE nova.
3. Usar `prepare_independent_owners.prepare` com sua assinatura vigente; sua
   mesclagem interna já fixa `migrate_existing_native_profiles=False`.
   Registrar **aditivamente** pelo operador transacional vigente sob
   `scan.lock`, sem substituir perfis, licenças, dados, modos e IDs anteriores.
4. Devolver recibo aplicado e capabilities assinadas com os **quatro IDs R149
   exatos**. Só então testar sala, boot, controles distintos, perda de sinal e
   saída com participantes no mesmo APK R149.

Não executar `main` de operadores antigos, reaplicar R141/QoS, fazer migração
genérica, deploy ou restart da API para este cadastro. A divisão de banda é
assunto separado; ver [diagnóstico dos downloads](../network-qos/DIAGNOSTICO-DOWNLOADS-ANTES-DEPOIS-20261014.md).

| Plataforma | ID R149 a conferir no recebimento |
| --- | --- |
| PSX | `pcsx-rearmed-ff81ed17-player-v3-29c3d7b9e6a8` |
| N64 | `mupen64plus-ae-33bf7021-n64-v3-dcad075758e5` |
| GameCube | `dolphin-5102a033-gamecube-v3-c80ec04d1723` |
| Wii | `dolphin-5102a033-wii-v3-c80ec04d1723` |
