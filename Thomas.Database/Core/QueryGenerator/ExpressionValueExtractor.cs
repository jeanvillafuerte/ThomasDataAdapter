﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Thomas.Database.Core.QueryGenerator
{
    internal class ExpressionValueExtractor<T> where T : class, new()
    {
        private readonly IParameterHandler _parameterHandler;
        private readonly Type _typeString;

        public ExpressionValueExtractor(IParameterHandler parameterHandler)
        {
            _parameterHandler = parameterHandler;
            _typeString = typeof(string);
        }

        internal bool LoadParameterValues(Expression expression)
        {
            return expression switch
            {
                ConstantExpression constantExpression => HandleConstantExpression(constantExpression),
                UnaryExpression unaryExpression => HandleUnaryExpression(unaryExpression),
                BinaryExpression binaryExpression => HandleBinaryExpression(binaryExpression),
                MemberExpression memberExpression when memberExpression.Type == typeof(string) => true,
                MemberExpression memberExpression when memberExpression.Type == typeof(int) ||
                                                       memberExpression.Type == typeof(short) ||
                                                       memberExpression.Type == typeof(long) ||
                                                       memberExpression.Type == typeof(decimal) ||
                                                       memberExpression.Type == typeof(DateTime) ||
                                                       memberExpression.Type == typeof(float) ||
                                                       memberExpression.Type.IsArray ||
                                                       (memberExpression.Type.IsGenericType &&
                                                       typeof(IEnumerable).IsAssignableFrom(memberExpression.Type)) => HandleMemberExpression(memberExpression),
                NewExpression newExpression => HandleNewExpression(newExpression),
                NewArrayExpression newArrayExpression => HandleNewArrayExpression(newArrayExpression),
                MemberExpression memberExpression when memberExpression.Type == typeof(bool) => true,
                MethodCallExpression methodCall when SqlGenerator<T>.IsStringContains(methodCall) => HandleStringContains(methodCall, StringOperator.Contains),
                MethodCallExpression methodCall when SqlGenerator<T>.IsEnumerableContains(methodCall) => HandleEnumerableContains(methodCall),
                MethodCallExpression methodCall when SqlGenerator<T>.IsEquals(methodCall) => HandleStringContains(methodCall, StringOperator.Equals),
                MethodCallExpression methodCall when SqlGenerator<T>.IsStartsWith(methodCall) => HandleStringContains(methodCall, StringOperator.StartsWith),
                MethodCallExpression methodCall when SqlGenerator<T>.IsEndsWith(methodCall) => HandleStringContains(methodCall, StringOperator.EndsWith),
                MethodCallExpression methodCall when SqlGenerator<T>.IsBetween(methodCall) => HandleBetween(methodCall),
                MethodCallExpression methodCall when SqlGenerator<T>.IsExists(methodCall) => HandleExists(methodCall),
                LambdaExpression lambdaExpression => LoadParameterValues(lambdaExpression.Body),
                MethodCallExpression methodCall => throw new NotSupportedException(),
                ListInitExpression listInitExpression => throw new NotSupportedException(),
                DynamicExpression dynamicExpression => throw new NotSupportedException(),
                ConditionalExpression conditionalExpression => throw new NotSupportedException(),
                GotoExpression gotoExpression => throw new NotSupportedException(),
                IndexExpression indexExpression => throw new NotSupportedException(),
                InvocationExpression invocationExpression => throw new NotSupportedException(),
                LabelExpression labelExpression => throw new NotSupportedException(),
                LoopExpression loopExpression => throw new NotSupportedException(),
                MemberInitExpression memberInitExpression => throw new NotSupportedException(),
                SwitchExpression switchExpression => throw new NotSupportedException(),
                TryExpression tryExpression => throw new NotSupportedException(),
                TypeBinaryExpression typeBinaryExpression => throw new NotSupportedException(),
                _ => throw new NotImplementedException(),
            };
        }

        private bool HandleNewExpression(NewExpression newExpression)
        {
            if (newExpression.Type == typeof(DateTime))
            {
                var instantiator = Expression.Lambda<Func<DateTime>>(newExpression).Compile();
                var value = instantiator();
                _parameterHandler.AddInParam(typeof(DateTime), value, out var _);
            }

            return true;
        }

        private bool HandleMemberExpression(MemberExpression memberExpression)
        {
            if (memberExpression.Expression is ConstantExpression ce && memberExpression.Member is FieldInfo fe)
            {
                var constant = Expression.Constant(fe.GetValue(ce.Value));
                if (constant.Type.IsArray || (constant.Type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(constant.Type)))
                {
                    var @params = new List<string>();

                    if (typeof(IEnumerable<int>).IsAssignableFrom(constant.Type))
                        AddArrayConstantValues<int>(typeof(int), constant);
                    else if (typeof(IEnumerable<short>).IsAssignableFrom(constant.Type))
                        AddArrayConstantValues<short>(typeof(short), constant);
                    else if (typeof(IEnumerable<long>).IsAssignableFrom(constant.Type))
                        AddArrayConstantValues<long>(typeof(long), constant);
                    else if (typeof(IEnumerable<decimal>).IsAssignableFrom(constant.Type))
                        AddArrayConstantValues<decimal>(typeof(decimal), constant);
                    else if (typeof(IEnumerable<float>).IsAssignableFrom(constant.Type))
                        AddArrayConstantValues<float>(typeof(float), constant);
                    else if (typeof(IEnumerable<DateTime>).IsAssignableFrom(constant.Type))
                        AddArrayConstantValues<DateTime>(typeof(DateTime), constant);

                    return true;
                }
                else
                {
                    _parameterHandler.AddInParam(constant.Type, constant.Value, out var _);
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        private IEnumerable<T> GetValues<T>(ConstantExpression expression)
        {
            var instantiator = Expression.Lambda<Func<IEnumerable<T>>>(expression).Compile();
            return instantiator();
        }

        private void AddArrayConstantValues<T>(Type type, ConstantExpression expression)
        {
            foreach (var value in GetValues<T>(expression))
                _parameterHandler.AddInParam(type, value, out var _);
        }

        private bool HandleConstantExpression(ConstantExpression constantExpression)
        {
            if (constantExpression.Value == null)
                return true;
            else
            {
                _parameterHandler.AddInParam(constantExpression.Type, constantExpression.Value, out var _);
                return true;
            }
        }

        private bool HandleUnaryExpression(UnaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.Not:
                    return LoadParameterValues(expression.Operand);
                default:
                    throw new NotSupportedException("Unsupported unary operator");
            }
        }

        private bool HandleBinaryExpression(BinaryExpression expression)
        {
            LoadParameterValues(expression.Left);
            LoadParameterValues(expression.Right);
            return true;
        }

        private bool HandleNewArrayExpression(NewArrayExpression newArrayExpression)
        {
            for (int i = 0; i < newArrayExpression.Expressions.Count; i++)
                LoadParameterValues(newArrayExpression.Expressions[i]);

            return true;
        }

        private bool HandleStringContains(MethodCallExpression expression, StringOperator @operator)
        {
            if (expression.Object is MemberExpression memberExpression
                && expression.Arguments[0] is ConstantExpression constantExpression)
            {
                var value = constantExpression.Value.ToString();
                _parameterHandler.AddInParam(in _typeString, value, out var _);
                return true;
            }
            else if (expression.Object is MemberExpression memberExpression2
                && expression.Arguments[0] is ConstantExpression constantExpression2
                && @operator == StringOperator.Equals)
            {
                return LoadParameterValues(constantExpression2);
            }

            throw new NotSupportedException("Unsupported method call");
        }

        private bool HandleEnumerableContains(MethodCallExpression expression)
        {
            if (expression.Arguments.Count == 2 || expression.Arguments.Count == 1)
            {
                LoadParameterValues(expression.Arguments[0]);
                LoadParameterValues(expression.Arguments[1]);
            }
            return true;
        }

        private bool HandleBetween(MethodCallExpression methodCall)
        {
            var expression = (methodCall.Arguments[0] as LambdaExpression).Body as UnaryExpression;
            var minValue = methodCall.Arguments[1] is MemberExpression memberExpression ? true : LoadParameterValues(methodCall.Arguments[1]);
            var maxValue = methodCall.Arguments[2] is MemberExpression memberExpression2 ? true : LoadParameterValues(methodCall.Arguments[2]);
            return LoadParameterValues(expression.Operand);
        }

        private bool HandleExists(MethodCallExpression methodCall)
        {
            var lambdaExpression = methodCall.Arguments[0] as LambdaExpression;

            if (lambdaExpression?.Body != null)
                return LoadParameterValues(lambdaExpression.Body);

            return true;
        }
    }
}
