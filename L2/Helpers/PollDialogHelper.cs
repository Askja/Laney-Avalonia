using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ELOR.Laney.Core;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public static class PollDialogHelper {
        private const int PollAnswerSlots = 10;

        public static async Task<Poll> CreatePollWithDialogAsync(VKSession session, Window owner, long ownerId) {
            PollCreateDialogState state = BuildPollCreateContent();
            VKUIDialog dialog = new VKUIDialog("Создать опрос", "Минимум два варианта ответа. Пустые варианты просто игнорируются, без этого балагана.", [Assets.i18n.Resources.close, Assets.i18n.Resources.add], 2) {
                DialogContent = state.Content
            };

            if (await dialog.ShowDialog<int>(owner) != 2) return null;

            string question = state.Question.Text?.Trim();
            List<string> answers = state.Answers
                .Select(a => a.Text?.Trim())
                .Where(a => !String.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(PollAnswerSlots)
                .ToList();

            if (String.IsNullOrWhiteSpace(question) || answers.Count < 2) {
                VKUIDialog alert = new VKUIDialog("Опрос не создан", "Нужен вопрос и хотя бы два нормальных варианта ответа. Да, VK без этого тоже не магичит.");
                await alert.ShowDialog(owner);
                return null;
            }

            try {
                Task<Poll> createTask = session.API.Polls.CreateAsync(
                    question,
                    answers,
                    state.Anonymous.IsChecked == true,
                    state.Multiple.IsChecked == true,
                    state.DisableUnvote.IsChecked == true,
                    ownerId: ownerId
                );
                return await new VKUIWaitDialog<Poll>().ShowAsync(owner, createTask);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(owner, ex, true);
                return null;
            }
        }

        public static async Task ShowPollByIdAsync(VKSession session, long ownerId, int pollId) {
            if (session == null) return;

            try {
                Poll poll = await LoadPollAsync(session, ownerId, pollId);
                await ShowPollAsync(session, poll);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }
        }

        public static async Task ShowPollAsync(VKSession session, Poll poll) {
            if (session == null || poll == null) return;

            Poll current = poll;
            while (current != null) {
                PollVoteDialogState state = BuildPollViewContent(current);
                List<ulong> selectedAnswerIds = current.AnswerIds ?? new List<ulong>();
                bool hasUserVote = selectedAnswerIds.Count > 0;
                bool canVote = current.CanVote && !current.Closed && !hasUserVote;
                bool canUnvote = hasUserVote && !current.DisableUnvote && !current.Closed;
                string[] buttons = canVote || canUnvote
                    ? [Assets.i18n.Resources.close, canUnvote ? "Отменить голос" : "Проголосовать"]
                    : [Assets.i18n.Resources.close];

                VKUIDialog dialog = new VKUIDialog(Assets.i18n.Resources.poll, null, buttons, buttons.Length) {
                    DialogContent = state.Content
                };

                int result = await dialog.ShowDialog<int>(session.ModalWindow);
                if (result != 2) return;

                try {
                    if (canUnvote) {
                        await new VKUIWaitDialog<bool>().ShowAsync(session.ModalWindow,
                            session.API.Polls.DeleteVoteAsync(current.OwnerId, current.Id, selectedAnswerIds, accessKey: current.AccessKey));
                    } else {
                        List<ulong> voteAnswerIds = state.GetSelectedAnswerIds();
                        if (voteAnswerIds.Count == 0) {
                            VKUIDialog alert = new VKUIDialog(Assets.i18n.Resources.poll, "Выбери хотя бы один вариант. Телепатия в API пока не завезена.");
                            await alert.ShowDialog(session.ModalWindow);
                            continue;
                        }

                        await new VKUIWaitDialog<bool>().ShowAsync(session.ModalWindow,
                            session.API.Polls.AddVoteAsync(current.OwnerId, current.Id, voteAnswerIds, accessKey: current.AccessKey));
                    }

                    current = await LoadPollAsync(session, current.OwnerId, current.Id, current.AccessKey);
                } catch (Exception ex) {
                    Log.Warning(ex, "Unable to update poll {OwnerId}_{PollId}", current.OwnerId, current.Id);
                    await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                    return;
                }
            }
        }

        private static async Task<Poll> LoadPollAsync(VKSession session, long ownerId, int pollId, string accessKey = null) {
            return await new VKUIWaitDialog<Poll>().ShowAsync(session.ModalWindow,
                session.API.Polls.GetByIdAsync(ownerId, pollId, true, VKAPIHelper.Fields));
        }

        private static PollCreateDialogState BuildPollCreateContent() {
            PollCreateDialogState state = new PollCreateDialogState();

            StackPanel root = new StackPanel {
                Spacing = 10
            };

            state.Question = new TextBox {
                MaxLength = 255,
                MinHeight = 40,
                TextWrapping = TextWrapping.Wrap
            };
            root.Children.Add(CreateCaption("Вопрос"));
            root.Children.Add(state.Question);
            root.Children.Add(CreateCaption("Варианты ответа"));

            StackPanel answers = new StackPanel {
                Spacing = 8
            };
            for (int i = 0; i < PollAnswerSlots; i++) {
                TextBox answer = new TextBox {
                    MaxLength = 100,
                    MinHeight = 36
                };
                state.Answers.Add(answer);

                Grid row = new Grid {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    ColumnSpacing = 8
                };
                row.Children.Add(new TextBlock {
                    Text = $"{i + 1}.",
                    Width = 24,
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetColumn(answer, 1);
                row.Children.Add(answer);
                answers.Children.Add(row);
            }
            root.Children.Add(new ScrollViewer {
                MaxHeight = 260,
                Content = answers
            });

            state.Anonymous = new ToggleSwitch {
                IsChecked = true
            };
            state.Multiple = new ToggleSwitch();
            state.DisableUnvote = new ToggleSwitch();
            root.Children.Add(BuildToggle("Анонимный опрос", "Список участников не будет торчать наружу.", state.Anonymous));
            root.Children.Add(BuildToggle("Несколько вариантов", "Можно выбрать больше одного ответа.", state.Multiple));
            root.Children.Add(BuildToggle("Запретить переголосование", "Жестко, но иногда надо.", state.DisableUnvote));

            state.Content = root;
            return state;
        }

        private static PollVoteDialogState BuildPollViewContent(Poll poll) {
            PollVoteDialogState state = new PollVoteDialogState();
            List<ulong> selectedAnswerIds = poll.AnswerIds ?? new List<ulong>();
            bool hasUserVote = selectedAnswerIds.Count > 0;
            bool canChoose = poll.CanVote && !poll.Closed && !hasUserVote;

            StackPanel root = new StackPanel {
                Spacing = 12,
                MinWidth = 360
            };

            TextBlock question = new TextBlock {
                Text = poll.Question,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.SemiBold
            };
            question.Classes.Add("Headline");
            root.Children.Add(question);
            root.Children.Add(CreateCaption(GetPollSummary(poll)));

            if (poll.Answers == null || poll.Answers.Count == 0) {
                root.Children.Add(CreateCaption("Ответов нет. VK опять сделал вид, что так и надо."));
            } else {
                StackPanel answers = new StackPanel {
                    Spacing = 10
                };

                foreach (PollAnswer answer in poll.Answers) {
                    Control selector = null;
                    bool isSelected = selectedAnswerIds.Contains(answer.Id);
                    if (canChoose) {
                        ToggleButton choice = poll.Multiple
                            ? new CheckBox()
                            : new RadioButton { GroupName = $"poll_{poll.OwnerId}_{poll.Id}" };
                        choice.Tag = answer.Id;
                        choice.VerticalAlignment = VerticalAlignment.Center;
                        state.Choices.Add(choice);
                        selector = choice;
                    }

                    Grid row = new Grid {
                        ColumnDefinitions = new ColumnDefinitions(selector == null ? "*" : "Auto,*"),
                        ColumnSpacing = 8
                    };

                    if (selector != null) {
                        row.Children.Add(selector);
                    }

                    StackPanel answerContent = new StackPanel {
                        Spacing = 4
                    };
                    if (selector != null) Grid.SetColumn(answerContent, 1);

                    Grid textRow = new Grid {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        ColumnSpacing = 8
                    };
                    TextBlock answerText = new TextBlock {
                        Text = isSelected ? $"✓ {answer.Text}" : answer.Text,
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = isSelected ? FontWeight.SemiBold : FontWeight.Normal
                    };
                    textRow.Children.Add(answerText);
                    TextBlock answerMeta = CreateCaption($"{FormatRate(answer.Rate)} · {FormatVotes(answer.Votes)}");
                    Grid.SetColumn(answerMeta, 1);
                    textRow.Children.Add(answerMeta);
                    answerContent.Children.Add(textRow);

                    answerContent.Children.Add(new ProgressBar {
                        Minimum = 0,
                        Maximum = 100,
                        Value = Math.Clamp(answer.Rate, 0, 100),
                        Height = 6,
                        IsHitTestVisible = false
                    });

                    row.Children.Add(answerContent);
                    answers.Children.Add(row);
                }

                root.Children.Add(new ScrollViewer {
                    MaxHeight = 360,
                    Content = answers
                });
            }

            state.Content = root;
            return state;
        }

        private static Grid BuildToggle(string header, string subtitle, ToggleSwitch toggle) {
            Grid row = new Grid {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 12,
                Margin = new Thickness(0, 2)
            };

            StackPanel text = new StackPanel {
                Spacing = 2
            };
            text.Children.Add(new TextBlock {
                Text = header,
                TextWrapping = TextWrapping.Wrap
            });
            text.Children.Add(CreateCaption(subtitle));

            Grid.SetColumn(toggle, 1);
            row.Children.Add(text);
            row.Children.Add(toggle);
            return row;
        }

        private static TextBlock CreateCaption(string text) {
            TextBlock caption = new TextBlock {
                Text = text,
                TextWrapping = TextWrapping.Wrap
            };
            caption.Classes.Add("Caption1");
            return caption;
        }

        private static string GetPollSummary(Poll poll) {
            List<string> parts = new List<string> {
                FormatVotes(poll.Votes),
                poll.Anonymous ? "анонимный" : "публичный"
            };
            if (poll.Multiple) parts.Add("несколько вариантов");
            if (poll.Closed) {
                parts.Add("закрыт");
            } else if (poll.EndDateUnix > 0) {
                parts.Add($"до {poll.EndDate:g}");
            }
            if (poll.AnswerIds != null && poll.AnswerIds.Count > 0) parts.Add("вы уже голосовали");
            return String.Join(" · ", parts);
        }

        private static string FormatRate(double rate) {
            return $"{rate.ToString("0.#", CultureInfo.CurrentCulture)}%";
        }

        private static string FormatVotes(int votes) {
            return $"{votes} голосов";
        }

        private sealed class PollCreateDialogState {
            public Control Content { get; set; }
            public TextBox Question { get; set; }
            public List<TextBox> Answers { get; } = new List<TextBox>();
            public ToggleSwitch Anonymous { get; set; }
            public ToggleSwitch Multiple { get; set; }
            public ToggleSwitch DisableUnvote { get; set; }
        }

        private sealed class PollVoteDialogState {
            public Control Content { get; set; }
            public List<ToggleButton> Choices { get; } = new List<ToggleButton>();

            public List<ulong> GetSelectedAnswerIds() {
                return Choices
                    .Where(c => c.IsChecked == true)
                    .Select(c => (ulong)c.Tag)
                    .ToList();
            }
        }
    }
}
