# Candidata PSX R172: antecedência de comandos

APK assinado exato: `4a92627952a4f9673ff402667825fcb1f926aaf85a44bef7882559f000c56c5b`,
fonte congelada do app `8317792fa231831f788cb10b7061bb11bc9fa97d`, versão14.
Kit privado: `cd0a4e43bd48a04cc5b8fc72e9a8236f737f13ed0e46a5eb556774d1d8e326be`.
Não há APK, ROM, BIOS, chave ou logs privados neste diretório.

O único delta funcional do app é a antecedência PSX de quatro para oito quadros,
com cerca de 67ms adicionais de atraso nominal no controle. Não há frameskip,
redução de clock, troca de core/BIOS, novo processo, thread ou renderização.
Modelo do protocolo real reproduziu a limitação; efeito nos celulares ainda
precisa de prova. Fonte e evidência sanitizada estão no módulo
`integration/psx-input-buffer-r172` do app. N64 continua com a mesma lógica.

O preflight prevê quatro owners e492 perfis novos, conservando18.372 perfis,
103 owners e cada aprovação/recusa/modo por conteúdo. Reutiliza492 decisões
preparadas acrescentando seus owners exatos à lista `engineIds`, sem duplicar
texto ou mudar IDs e campos dos modos. Total previsto:107 owners,18.864 perfis,
13.457 modos; catálogo100/4.744 preservado. Perfil PSX novo:
`86f543015f84ca292c6853d10cbc58e4869b825520dbff117a4868bf18637683`.

O registrador aceita somente extensão das listas de owners: todos os campos de
decisão e os owners anteriores permanecem exatos; owner recebido, plataforma e
controlador precisam corresponder. A validação de capacidade usa os bytes UTF-8
compactos efetivamente gravados pelo registrador e scanner. Limites continuam
18MiB para perfis e9MiB para modos. Preflight:16.419.134B e9.176.895B.
Não houve aumento de limite na API ou necessidade de restart.

Testes específicos: nove de imutabilidade/vínculos, dois de capacidade, quatro
de histórico/perfis e oito recusas da projeção com dados reais. Preparação pura
em `operator/prepare_all.py`; não executa implantação. A autorização Linux, o
recibo de aplicação e as consultas assinadas reais são etapas distintas do
preflight. Não considerar o cadastro aplicado apenas por este documento.

Instalar a mesma candidata nos dois aparelhos somente fora da partida e
preservando os dados. Registrar velocidade nativa, áudio, controles, saída e
temperatura. Não publicar no atualizador geral nem promover a estável antes
da comparação física. A R171 atual dos dois teve desempenho reprovado.
