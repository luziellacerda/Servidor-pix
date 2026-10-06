# Convite curto e palavra passe automática — Station — 06/10/2026

## Entrega candidata

Solicitação: convidado entra sem digitar a senha do motor; código de convite de sala com8caracteres, apresentado como `7KPM-4XRT`. Fontes sobre R55+visualR57+prontidãof8b019d6 em TurboElden, `versions/station-auto-room-access-r57-20261006`. APK baseR57/e6159fa3 e certificado original7b16 preservados. O APK novo ainda precisa ser compilado/assinado no PC e validado nos dois aparelhos.

Causa localizada no RetroArch fixado69a4f0ea: receber salt não zero abria `menu_input_dialog_start`, sem usar a senha já configurada pelo app. Novo runtime usa a credencial privada só no cliente Android tipado; mantém salt, SHA256, NICK antes de PASSWORD, verificação do anfitrião e recusa de senha incorreta. SHA256 **899e35279cf046242a7ae31f502078080bd6a94404770fe99e1e2e90be58a1ef**, arm64/API26/NDKr28c,10.666.496bytes/alinhamento16KiB, flags originais sem Vulkan/Cheevos/SAF. Fonte/patch/receita/licença estão publicados junto ao binário GPL no app; nenhum APK/ROM/BIOS/chave é publicado.

## Contrato aditivo

O snapshot assinado de `/v1/station/online/command` anuncia `roomCapabilities:["short-invite-v1"]`. `room.inviteCode` contém8caracteres aleatórios de `23456789ABCDEFGHJKLMNPQRSTUVWXYZ`, único entre salas vivas. O código localiza uma sala pública e não substitui a autenticação.

```
POST /v1/station/online/command
Authorization: Bearer <sessao-do-aparelho>
{"action":"resolve-code","requestId":"<UUID-novo>","inviteCode":"7KPM-4XRT"}
```

A resposta mantém a assinatura/domínio e vínculos existentes. `snapshot.resolvedRoom` contém apenas `roomId`, `itemId`, `engineId`, `hostId`; `room` continua nulo para quem não entrou. Depois da preparação do jogo, o app faz o `join` original com hashes. A senha chega automaticamente na resposta assinada de membro. Convite direto de pessoa permanece Aceitar e entrar; Pronto e start mantêm as regras de dois jogadores.

Código encerrado/expirado/revogado/reinício:404 `STATION_ONLINE_CODE_NOT_FOUND`; formato inválido400 `STATION_ONLINE_CODE_INVALID`; sala cheia/em partida409 `STATION_ONLINE_ROOM_FULL`; bloqueio403; sessão ausente401; tentativas entram no limite existente30ações/10s. Aceita minúsculas, hífen central opcional e espaços externos, com tamanho limitado. Mapeamento somente em memória, eliminado com a sala. Convites TS1 anteriores continuam aceitos pelo app.

Registro de motores em `short-invite-20261006/online-engine-registry.json`: dois registros originais mantidos e dois IDs `-autopass1` acrescentados com o runtime acima; cores/opções/controles intactos. Aparelhos antigos continuam com seus motores. Os dois aparelhos do teste automático precisam da nova edição.

## Evidência e limites

318 códigos/salas,41 salas anteriores,34 sociais e53 HTTP com assinatura/autenticação sintéticas passaram. JVM278, trecho nativo66, TCP/TLS/relay39 com12.583.029bytes/direção, compilação158Java8/API34 em api-check-only, patch e recusas de empacotamento passaram. Motor Android compilado e ELF conferido; não executado em aparelho. Novo APK/gameplay ainda não comprovados.

As provas publicadas em `short-invite-20261006/` referenciam os fontes do app; não são um snapshot Android autossuficiente. Não reintroduzir R41 nem sobrepor o layout/saída/canal atuais.

## Publicação preparada

Script guardado: `scripts/implantar-convite-curto-station-20261006.py`. Alvo único `turborama-station-api.service`, baselinea2bb176/DLLd181bf97,512salas/1024conexões. Preserva catálogo14/2212, licenças, chaves, proxy, scanner/mídias, painel, PIX/Suite/ES e serviços compartilhados. Sem migration; registro aditivo em nova release, sem alterar o arquivo antigo. Exige fonte limpa/artefato por hash, ausência de partidas relay ativas, backup restaurado, provas autenticadas em porta sombra e HTTPS público, limpeza sintética e comparação de licenças reais. Retorno remove somente o novo drop-in e devolve a2bb176+registro original; recusa sucessoras.

**Implantação ainda não confirmada nesta etapa de preparo.** O recibo final indicará commit/DLL/PID, registro e verificações realmente publicados. Depois, no PC seguir as receitas do novo candidato, empacotar as três entradas e atualizar ambos mantendo dados; testar Battletoads, código/pessoa, ausência da palavra passe, inputs e saída/retorno.
