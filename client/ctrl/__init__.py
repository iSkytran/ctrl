from .ctrl_env import CtrlEnv
from gymnasium.envs.registration import register

register(
    id="ctrl",
    entry_point="ctrl.ctrl_env:CtrlEnv",
    nondeterministic=True
)
