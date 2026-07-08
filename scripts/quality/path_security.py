#!/usr/bin/env python3
import pathlib


class PathSecurityError(ValueError):
    pass


def repo_root() -> pathlib.Path:
    return pathlib.Path(__file__).resolve().parents[2]


def quality_scripts_root() -> pathlib.Path:
    return repo_root() / "scripts" / "quality"


def artifacts_root() -> pathlib.Path:
    return repo_root() / "artifacts"


def downloaded_sonar_artifacts_root() -> pathlib.Path:
    return repo_root() / "downloaded-sonar-artifacts"


def _is_relative_to(path: pathlib.Path, root: pathlib.Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def _candidate_path(value: str | pathlib.Path, base_dir: pathlib.Path) -> pathlib.Path:
    path = pathlib.Path(value)
    return path if path.is_absolute() else base_dir / path


def _resolved_allowed_root(allowed_root: pathlib.Path) -> pathlib.Path:
    root = pathlib.Path(allowed_root)
    if root.exists() and root.is_symlink():
        raise PathSecurityError(f"raiz permitida nao pode ser symlink: {root}.")
    return root.resolve(strict=False)


def _ensure_within_root(path: pathlib.Path, allowed_root: pathlib.Path, label: str) -> None:
    if not _is_relative_to(path, allowed_root):
        raise PathSecurityError(
            f"{label} fora da raiz permitida: {path}. Raiz permitida: {allowed_root}."
        )


def _resolve_path(
    value: str | pathlib.Path,
    allowed_root: pathlib.Path,
    *,
    base_dir: pathlib.Path | None = None,
    must_exist: bool,
    label: str,
) -> pathlib.Path:
    resolved_root = _resolved_allowed_root(allowed_root)
    candidate = _candidate_path(value, base_dir or repo_root())
    try:
        resolved_path = candidate.resolve(strict=must_exist)
    except FileNotFoundError as exc:
        raise PathSecurityError(f"{label} inexistente: {candidate}.") from exc
    except OSError as exc:
        raise PathSecurityError(f"{label} nao pode ser resolvido com seguranca: {candidate}.") from exc

    _ensure_within_root(resolved_path, resolved_root, label)
    return resolved_path


def resolve_existing_file(
    value: str | pathlib.Path,
    allowed_root: pathlib.Path,
    *,
    base_dir: pathlib.Path | None = None,
    label: str = "arquivo de entrada",
) -> pathlib.Path:
    path = _resolve_path(value, allowed_root, base_dir=base_dir, must_exist=True, label=label)
    if not path.is_file():
        raise PathSecurityError(f"{label} nao e um arquivo regular: {path}.")
    return path


def resolve_existing_dir(
    value: str | pathlib.Path,
    allowed_root: pathlib.Path,
    *,
    base_dir: pathlib.Path | None = None,
    label: str = "diretorio de entrada",
) -> pathlib.Path:
    path = _resolve_path(value, allowed_root, base_dir=base_dir, must_exist=True, label=label)
    if not path.is_dir():
        raise PathSecurityError(f"{label} nao e um diretorio: {path}.")
    return path


def resolve_output_file(
    value: str | pathlib.Path,
    allowed_root: pathlib.Path,
    *,
    base_dir: pathlib.Path | None = None,
    label: str = "arquivo de saida",
) -> pathlib.Path:
    return _resolve_path(value, allowed_root, base_dir=base_dir, must_exist=False, label=label)


def resolve_output_dir(
    value: str | pathlib.Path,
    allowed_root: pathlib.Path,
    *,
    base_dir: pathlib.Path | None = None,
    label: str = "diretorio de saida",
) -> pathlib.Path:
    return _resolve_path(value, allowed_root, base_dir=base_dir, must_exist=False, label=label)
