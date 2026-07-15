#!/usr/bin/env python3
"""Collect files included in commits received by Git's pre-push hook."""

from __future__ import annotations

import argparse
import os
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


ZERO_SHA = "0000000000000000000000000000000000000000"


@dataclass(frozen=True)
class PushRef:
    local_ref: str
    local_sha: str
    remote_ref: str
    remote_sha: str


@dataclass(frozen=True)
class BaseResult:
    base_ref: str
    merge_base: str


class GitError(RuntimeError):
    pass


def run_git(*args: str, check: bool = True) -> subprocess.CompletedProcess[bytes]:
    git_command = os.environ.get("PRE_PUSH_GIT", "git")
    result = subprocess.run(
        [git_command, *args],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )

    if check and result.returncode != 0:
        command = "git " + " ".join(args)
        stderr = result.stderr.decode("utf-8", errors="replace").strip()
        raise GitError(f"{command} falhou: {stderr or 'sem mensagem de erro'}")

    return result


def git_text(*args: str) -> str:
    return run_git(*args).stdout.decode("utf-8", errors="replace").strip()


def verify_commit(ref: str) -> str | None:
    result = run_git("rev-parse", "--verify", f"{ref}^{{commit}}", check=False)
    if result.returncode != 0:
        return None

    return result.stdout.decode("utf-8", errors="replace").strip()


def merge_base(head_ref: str, base_ref: str) -> str | None:
    result = run_git("merge-base", head_ref, base_ref, check=False)
    if result.returncode != 0:
        return None

    value = result.stdout.decode("utf-8", errors="replace").strip()
    return value or None


def validate_base_ref(base_ref: str, head_ref: str) -> tuple[BaseResult | None, str]:
    base_commit = verify_commit(base_ref)
    if base_commit is None:
        return None, "base nao resolve para um commit"

    head_commit = verify_commit(head_ref)
    if head_commit is None:
        return None, "SHA local nao resolve para um commit"

    common_base = merge_base(head_commit, base_commit)
    if common_base is None:
        return None, "nao existe merge-base entre a base e o SHA local"

    if common_base == head_commit:
        return None, "a base contem integralmente o SHA local"

    return BaseResult(base_ref=base_ref, merge_base=common_base), ""


def branch_name_from_local_ref(local_ref: str) -> str | None:
    prefix = "refs/heads/"
    if local_ref.startswith(prefix):
        return local_ref[len(prefix) :]

    return None


def upstream_ref(branch_name: str | None) -> str | None:
    target = f"{branch_name}@{{u}}" if branch_name else "@{u}"
    result = run_git("rev-parse", "--abbrev-ref", "--symbolic-full-name", target, check=False)
    if result.returncode != 0:
        return None

    value = result.stdout.decode("utf-8", errors="replace").strip()
    return value or None


def configured_tracking_ref(branch_name: str | None) -> str | None:
    if not branch_name:
        return None

    remote = run_git("config", f"branch.{branch_name}.remote", check=False)
    merge = run_git("config", f"branch.{branch_name}.merge", check=False)
    if remote.returncode != 0 or merge.returncode != 0:
        return None

    remote_name = remote.stdout.decode("utf-8", errors="replace").strip()
    merge_ref = merge.stdout.decode("utf-8", errors="replace").strip()
    if not remote_name or not merge_ref.startswith("refs/heads/"):
        return None

    if remote_name == ".":
        return merge_ref

    return f"{remote_name}/{merge_ref.removeprefix('refs/heads/')}"


def remote_head_refs() -> list[str]:
    result = run_git("for-each-ref", "--format=%(refname)", "refs/remotes", check=False)
    if result.returncode != 0:
        return []

    refs: list[str] = []
    for ref in result.stdout.decode("utf-8", errors="replace").splitlines():
        if not ref.endswith("/HEAD"):
            continue

        symbolic = run_git("symbolic-ref", "--short", ref, check=False)
        if symbolic.returncode == 0:
            value = symbolic.stdout.decode("utf-8", errors="replace").strip()
            if value:
                refs.append(value)

    return refs


def remote_short_refs() -> list[str]:
    result = run_git("for-each-ref", "--format=%(refname:short)", "refs/remotes", check=False)
    if result.returncode != 0:
        return []

    return [
        ref
        for ref in result.stdout.decode("utf-8", errors="replace").splitlines()
        if ref and not ref.endswith("/HEAD")
    ]


def resolve_best_remote_base(head_ref: str) -> tuple[BaseResult | None, str | None, str]:
    head_commit = verify_commit(head_ref)
    if head_commit is None:
        return None, None, "SHA local nao resolve para um commit"

    best_ref: str | None = None
    best_merge_base: str | None = None
    best_time = -1
    last_ref: str | None = None
    last_reason = "nenhuma ref remota candidata encontrada"

    for candidate_ref in remote_short_refs():
        last_ref = candidate_ref
        candidate_commit = verify_commit(candidate_ref)
        if candidate_commit is None:
            last_reason = "base nao resolve para um commit"
            continue

        candidate_merge_base = merge_base(head_commit, candidate_commit)
        if candidate_merge_base is None:
            last_reason = "nao existe merge-base entre a base e o SHA local"
            continue

        if candidate_merge_base == head_commit:
            last_reason = "a base contem integralmente o SHA local"
            continue

        timestamp_result = run_git("show", "-s", "--format=%ct", candidate_merge_base, check=False)
        timestamp_text = timestamp_result.stdout.decode("utf-8", errors="replace").strip()
        timestamp = int(timestamp_text or "0") if timestamp_result.returncode == 0 else 0
        if timestamp > best_time:
            best_ref = candidate_ref
            best_merge_base = candidate_merge_base
            best_time = timestamp

    if best_ref and best_merge_base:
        return BaseResult(base_ref=best_ref, merge_base=best_merge_base), best_ref, ""

    return None, last_ref, last_reason


def resolve_safe_base(head_ref: str, local_ref: str) -> tuple[BaseResult | None, str, str]:
    branch_name = branch_name_from_local_ref(local_ref)
    candidates: list[str] = []

    env_base_ref = os.environ.get("PRE_PUSH_BASE_REF", "").strip()
    if env_base_ref:
        candidates.append(env_base_ref)

    for candidate in (
        upstream_ref(branch_name),
        configured_tracking_ref(branch_name),
        *remote_head_refs(),
    ):
        if candidate and candidate not in candidates:
            candidates.append(candidate)

    last_attempt = env_base_ref or "nenhuma base candidata"
    last_reason = "nenhuma base candidata encontrada"
    for candidate in candidates:
        last_attempt = candidate
        resolved, reason = validate_base_ref(candidate, head_ref)
        if resolved:
            return resolved, candidate, ""

        last_reason = reason

    best, attempted, reason = resolve_best_remote_base(head_ref)
    if best:
        return best, best.base_ref, ""

    return None, attempted or last_attempt, reason or last_reason


def parse_push_refs(stdin_text: str) -> list[PushRef]:
    refs: list[PushRef] = []
    for line in stdin_text.splitlines():
        if not line.strip():
            continue

        parts = line.split()
        if len(parts) != 4:
            raise ValueError(f"entrada pre-push invalida: {line}")

        refs.append(PushRef(*parts))

    return refs


def diff_name_status(*diff_args: str) -> bytes:
    result = run_git("diff", "-C", "--find-copies-harder", "--name-status", "-z", *diff_args, check=False)
    if result.returncode != 0:
        stderr = result.stderr.decode("utf-8", errors="replace").strip()
        raise GitError(stderr or "git diff nao retornou detalhes")

    return result.stdout


def parse_name_status_z(data: bytes) -> list[tuple[bytes, ...]]:
    tokens = data.split(b"\0")
    if tokens and tokens[-1] == b"":
        tokens.pop()

    records: list[tuple[bytes, ...]] = []
    index = 0
    while index < len(tokens):
        status = tokens[index]
        index += 1

        if not status:
            continue

        change_kind = chr(status[:1][0])
        if change_kind in {"R", "C"}:
            if index + 1 >= len(tokens):
                raise ValueError("saida de git diff --name-status -z incompleta para rename/copy")
            old_path = tokens[index]
            new_path = tokens[index + 1]
            index += 2
            records.append((status, old_path, new_path))
            continue

        if index >= len(tokens):
            raise ValueError("saida de git diff --name-status -z incompleta")
        path = tokens[index]
        index += 1
        records.append((status, path))

    return records


def write_records(path: Path, records: list[tuple[bytes, ...]]) -> None:
    with path.open("wb") as output:
        for record in records:
            for field in record:
                output.write(field)
                output.write(b"\0")


def write_paths(path: Path, records: list[tuple[bytes, ...]]) -> None:
    seen: set[bytes] = set()
    with path.open("wb") as output:
        for record in records:
            for field in record[1:]:
                if field in seen:
                    continue

                seen.add(field)
                output.write(field)
                output.write(b"\n")


def collect_records(push_refs: list[PushRef]) -> list[tuple[bytes, ...]]:
    collected: list[tuple[bytes, ...]] = []
    seen_records: set[tuple[bytes, ...]] = set()

    if not push_refs:
        print("==> pre-push: tentando identificar branch de origem/base")
        base, attempted_base, reason = resolve_safe_base("HEAD", "")
        if base is None:
            raise GitError(
                "nao foi possivel calcular diff seguro para execucao manual; "
                "ref local: HEAD; "
                "SHA local: HEAD; "
                f"base tentada: {attempted_base}; "
                f"motivo: {reason}"
            )

        print(f"==> pre-push: base identificada: {base.base_ref}")
        print(f"==> pre-push: range analisado: {base.base_ref}...HEAD")
        data = diff_name_status(base.merge_base, "HEAD")
        return parse_name_status_z(data)

    for push_ref in push_refs:
        if push_ref.local_sha == ZERO_SHA:
            print(f"==> pre-push: ref removida detectada; ignorando {push_ref.remote_ref}")
            continue

        if push_ref.remote_sha and push_ref.remote_sha != ZERO_SHA:
            print("==> pre-push: branch remota existente detectada")
            print(f"==> pre-push: range analisado: {push_ref.remote_sha}..{push_ref.local_sha}")
            data = diff_name_status(f"{push_ref.remote_sha}..{push_ref.local_sha}")
        else:
            print("==> pre-push: branch remota ainda nao existe")
            print("==> pre-push: tentando identificar branch de origem/base")
            base, attempted_base, reason = resolve_safe_base(push_ref.local_sha, push_ref.local_ref)
            if base is None:
                raise GitError(
                    "nao foi possivel calcular diff seguro para branch nova; "
                    f"ref local: {push_ref.local_ref}; "
                    f"SHA local: {push_ref.local_sha}; "
                    f"base tentada: {attempted_base}; "
                    f"motivo: {reason}"
                )

            print(f"==> pre-push: base identificada: {base.base_ref}")
            print(f"==> pre-push: range analisado: {base.base_ref}...{push_ref.local_sha}")
            data = diff_name_status(base.merge_base, push_ref.local_sha)

        for record in parse_name_status_z(data):
            if record in seen_records:
                continue

            seen_records.add(record)
            collected.append(record)

    return collected


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path, help="arquivo que recebera registros NUL")
    parser.add_argument("--paths-output", type=Path, help="arquivo opcional com caminhos em linhas")
    args = parser.parse_args()

    try:
        push_refs = parse_push_refs(sys.stdin.read())
        records = collect_records(push_refs)
        write_records(args.output, records)
        if args.paths_output:
            write_paths(args.paths_output, records)
        return 0
    except (GitError, ValueError) as exc:
        print(f"==> pre-push: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
