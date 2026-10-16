using System;
using System.Collections;
using System.Collections.Generic;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 연출 코드(IEnumerator)를 한 프레임씩 직접 실행하는 실행기입니다.
    /// Unity 코루틴과 달리 예외 · 중단 시점을 Director가 알 수 있어 항상 정리 작업을 실행할 수 있습니다.
    /// 지원하는 yield 값: null(다음 프레임), CutsceneHandle(끝날 때까지 대기), IEnumerator(그 자리에서 이어 실행).
    /// </summary>
    internal sealed class CutsceneRoutine
    {
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private CutsceneHandle _waiting;

        public bool IsFinished => _stack.Count == 0;

        public CutsceneRoutine(IEnumerator root)
        {
            if (root != null)
            {
                _stack.Push(root);
            }
        }

        /// <summary>
        /// 다음 대기 지점까지 실행합니다. 연출 코드에서 난 예외는 호출자에게 그대로 전달됩니다.
        /// </summary>
        public void Tick()
        {
            if (_waiting != null)
            {
                if (!_waiting.IsDone) return;
                _waiting = null;
            }

            while (_stack.Count > 0)
            {
                IEnumerator top = _stack.Peek();
                if (!top.MoveNext())
                {
                    _stack.Pop();
                    continue;
                }

                switch (top.Current)
                {
                    case null:
                        return;
                    case CutsceneHandle handle:
                        if (handle.IsDone) continue;
                        _waiting = handle;
                        return;
                    case IEnumerator nested:
                        _stack.Push(nested);
                        continue;
                    default:
                        throw new InvalidOperationException(
                            $"컷씬 연출 코드에서 지원하지 않는 yield 값입니다: {top.Current.GetType().Name}. " +
                            "WaitForSeconds 대신 c.Wait()처럼 컷씬 명령을 사용하세요.");
                }
            }
        }

        /// <summary>
        /// 실행을 중단합니다. 연출 코드의 finally 블록이 실행됩니다.
        /// </summary>
        public void Dispose()
        {
            _waiting = null;
            while (_stack.Count > 0)
            {
                if (_stack.Pop() is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
