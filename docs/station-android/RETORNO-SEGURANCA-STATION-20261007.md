# Segurança Station — correção de 07/10/2026

## Estado desta revisão

Publicada em **07/10/2026 às12h38Maceió (15h38UTC)**. Fonte de implantação **da073551428c4b1320a3abebc257b3c3c933b3ca**, implementação C#882a009, DLL **83c8d2b3da68c85da7402165a7fa915fa7a084125511ec2df5541ec36ecaf682**, PID **1147382**, usuário/grupo **turborama-station-api**. [Recibo agregado sem credenciais](SEGURANCA-STATION-PRODUCAO-20261007.json).

Backup PostgreSQL restaurado e configurações verificadas; ensaio com o mesmo usuário, papel e namespace definitivos passou. **4921 arquivos de mídia legíveis**, quatro caminhos protegidos inacessíveis e mídias/runtime somente para leitura. Papel sem administração, sem acesso às seis tabelas brutas verificadas e sem linhas de outros produtos nas visões. HTTPS público:198 verificações de salas/comunidade/convites e15 de proteção por pedido passaram; ensaio de relay:2554 checks, 2097269bytes por direção, quatro capas e catálogo completo. Limpeza das licenças/salas sintéticas confirmada. Origem direta404 mesmo com CF-Connecting-IP forjado; prontidão, capacidade512/1024 e todos os outros serviços preservados.

Catálogo rev14/2212visíveis/2467totais, quatro motores, chaves e licenças reais mantidos. Management910766/helper910776 mantidos; saúde do management conferida com token no socket privado. Sua porta5187 oculta saúde sem autenticação com404: não interpretar como serviço quebrado. Suite5190/gateway5191 saudáveis. **Sem mudança de firewall, SSH, Cloudflare ou reinício de outro produto.**

As tentativas anteriores foram interrompidas pelo publicador e retornadas automaticamente: normalização da saída SQL, leitura de migration pela pasta privada, permissão owner-only da chave simétrica, limite do verificador do catálogo, identificador Android exigido pelo filtro público e saúde protegida do painel. Esses pontos foram corrigidos no publicador antes da publicação final. Um relatório de memória do próprio ensaio foi removido após verificar seu comando; novos dumps desativados/filtrados somente nesta unidade. Nenhum dado real de cliente foi removido.

Base anterior preservada: APIa3e83d96/DLL5fff55c1. Fonte Android **213cfce** sobre029612b: R55 + visualR57 + prontidão + palavra passe automática + proteção. Novo APK e qualificação dos aparelhos continuam pendentes no PC de produção.

## Correções

- API Station com usuário Linux e papel PostgreSQL próprios; visões filtradas pelo produto e função de vínculo restrita. Sem credenciais, chave privada ou pepper da Suite dentro do processo Station.
- Processo com arquivos de sistema somente para leitura, diretórios dos outros sistemas inacessíveis, mídias compiladas permitidas e leitura das importações futuras por ACL. Chaves Station existentes preservadas.
- Rotas Station atendidas pelo túnel local; acesso direto à origem recusado, inclusive com cabeçalho Cloudflare forjado. Outros produtos conservam as rotas atuais. Cabeçalho de prova encaminhado e limite de 64 KiB somente nos dois envelopes de autenticação.
- Pedidos autenticados e abertura WSS assinados pelo aparelho: método, rota, corpo, credencial, horário e nonce. Token ou ticket copiado e repetição são recusados. Depois da adesão assinada de um aparelho, uma sessão mais fraca é recusada. Nenhuma assinatura por frame ou nova verificação de integridade de ROM.
- Verificação opcional de chave Android atestada: raízes oficiais Google, revogação, pacote org.turboramastation.frontend, certificado original, boot verificado e propriedade da chave. Política global estrita desligada até qualificação dos aparelhos e atualização do APK.
- Compartilhamento Samba obsoleto específico passa a exigir autenticação; os outros compartilhamentos são preservados. Seu diretório está ausente: a simulação não demonstrou vazamento por esse compartilhamento.
- Catálogo com metadata=1 passa a enviar objeto vazio quando não existe sinopse; o cliente anterior não aceita metadata:null.

## Compatibilidade e limites

Migrations 031/032 aditivas, sem apagar licenças ou conteúdo. O retorno restaura identidade, configuração, ACL, proxy e executável anteriores e desativa o novo papel, preservando os dados que chegaram. Não se restaura o banco de produção sobre novas vendas. Scripts exigem DLL, catálogo, registro dos quatro motores, fonte limpa e ausência de partida ativa na troca; recusam sucessoras.

APKs antigos continuam utilizando seu protocolo atual. A prova por pedido protege somente os aparelhos que aderiram pelo APK atualizado. Enquanto Station:Security:RequireVerifiedApp=false, uma ativação válida por cliente próprio ainda é permitida: não afirmar exclusividade de APK nem segurança absoluta. Cópia de código válido não usado continua sendo uma credencial; manter a emissão de uso único/30 minutos e o vínculo existente.

A política de cadeia é estrita com validade de certificados. Aparelhos com certificados antigos expirados ou sem hardware atestado usam prova RSA compatível enquanto a política global estiver desligada; a raiz e a revogação continuam obrigatórias para o selo verifiedApp. Nunca forçar a política antes de testar os modelos reais. Chave RSA principal/identidade atual não é substituída. Recuperação de aparelho já protegido exige cliente atualizado ou retorno coordenado do backend, sem limpar dados ou desinstalar o app.

Firewall global, SSH, PIX, Suite Windows, ES Windows, site, WhatsApp, painel, bancos e gateway preservados. Não há motivo confirmado para fechar globalmente as portas compartilhadas. Capacidade do relay 512 salas/1024 conexões mantida; não comprova gameplay em centenas de celulares.

## Evidência de implementação e publicação

- Build .NET Release: zero erros/avisos; suíte completa incluindo contratos estritos, concorrência, produto e segurança criptográfica passou.
- PostgreSQL temporário: seis operações proibidas negadas ao novo papel; ativação, emissão, suspensão, transferência, revogação e limpeza sintéticas passaram.
- 28 verificações HTTP/WSS de proteção, bytes nas duas direções, rota/corpo/token/chave alterados, replay, downgrade e política estrita temporária passaram.
- Cliente Java real contra API candidata: ativação, renovação, catálogo, perfil, quatro capas simultâneas, download exato e prova de relay; 21 pedidos protegidos passaram.
- 190 fontes Java8/API34 compilaram; regressões cliente: 75 API, 13 reutilização de transferência, 41 concorrência de capas e 180 publicação de capas passaram.
- Verificador de publicação exercitado no PostgreSQL temporário; catálogo e limpeza passaram. Transformações preservam outras rotas/compartilhamentos e transferências sem buffering/900 segundos.

## Aplicativo e próximas etapas de produção Android

Delta em versions/station-security-r57-20261007 no TurboElden. Compor as fontes preservadas R55 + visual R57 + acesso automático + proteção. Compilar cliente/DEX28 e salas/DEX35 juntos, usando o novo cliente como classpath das salas. Empacotar apenas sobre APK R57 e6159fa3 com certificado original 7b16ee1a, runtime auto-password 899e3527 e seu engines.json. Conferir todos os outros arquivos, alinhamento 16 KiB, classes sem duplicação e atualização sem limpar dados.

O Linux não tem o APK privado, D8 e assinatura do PC de produção: novo DEX/APK, instalação, cadeia hardware e partida física continuam pendentes. Testar os dois aparelhos com quatro capas/download/renovação, ambos nomes na sala, Pronto, entrada automática, inputs e retorno, além dos casos de credencial copiada. Registrar versão/pacote/certificado e signed verifiedApp. Exigir APK original globalmente somente após todos os clientes necessários estarem qualificados ou existir política de migração operacional.

Referências: [Android key attestation](https://developer.android.com/privacy-and-security/security-key-attestation), [formato de atestação](https://source.android.com/docs/security/features/keystore/attestation), [proteção da origem Cloudflare](https://developers.cloudflare.com/fundamentals/security/protect-your-origin-server/). O verificador específico usa validade estrita e não substitui a qualificação de certificados antigos recomendada pelo Android.


## Retorno e retomada

Rollback guardado pelo script `scripts/implantar-seguranca-station-20261007.py --rollback --revision da073551428c4b1320a3abebc257b3c3c933b3ca`, usando o Python do operador e autenticação nativa. Originais e backup restaurado permanecem privados no servidor. O script recusa uma sucessora, partida ativa ou configurações divergentes; restaura somente as alterações desta publicação e desativa o novo papel. Migrations031/032 e dados novos são retidos; não restaurar backup de banco sobre novas vendas.

O worktree de implantação foi mantido limpo na revisão exata durante a execução; documentação e recibos posteriores ficam em worktree separado. Receitas históricas de implantação da API anterior não se aplicam à nova identidade. Atualizar ambos os telefones pelo novo overlay antes de exigir autenticidade global. Não trocar chaves, limpar dados ou desinstalar para testar.
