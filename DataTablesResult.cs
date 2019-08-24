using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic;
using System.Web;
using System.Web.Mvc;

namespace RootHelpers.Datatables
{
    public class DataTablesResult : JsonResult
    {
        public static DataTablesResult<TRes> Create<T, TRes>(IQueryable<T> q, DataTablesParam dataTableParam, DataTablesOptions dataTableOptions, Func<T, TRes> transform)
        {
            return new DataTablesResult<T, TRes>(q, dataTableParam, dataTableOptions, transform);
        }

        public static DataTablesResult<T> Create<T>(IQueryable<T> q, DataTablesParam dataTableParam)
        {
            return new DataTablesResult<T, T>(q, dataTableParam, null, t => t);
        }

        public static DataTablesResult<T> CreateResultUsingEnumerable<T>(IEnumerable<T> q, DataTablesParam dataTableParam)
        {
            return new DataTablesResult<T, T>(q.AsQueryable(), dataTableParam, null, t => t);
        }

        public static DataTablesResult Create(object queryable, DataTablesParam dataTableParam)
        {
            queryable = ((IEnumerable)queryable).AsQueryable();
            var s = "Create";

            var openCreateMethod =
                typeof(DataTablesResult).GetMethods().Single(x => x.Name == s && x.GetGenericArguments().Count() == 1);
            var queryableType = queryable.GetType().GetGenericArguments()[0];
            var closedCreateMethod = openCreateMethod.MakeGenericMethod(queryableType);
            return (DataTablesResult)closedCreateMethod.Invoke(null, new[] { queryable, dataTableParam });
        }
    }

    public class DataTablesResult<T> : DataTablesResult
    {
    }

    public class DataTablesResult<T, TRes> : DataTablesResult<TRes>
    {
        private readonly Func<T, TRes> _transform;

        public DataTablesResult(IQueryable<T> q, DataTablesParam dataTableParam, DataTablesOptions dataTableOptions, Func<T, TRes> transform)
        {
            _transform = transform;
            var properties = typeof(TRes).GetProperties();

            if (dataTableOptions == null)
            {
                dataTableOptions = new DataTablesOptions();
            }

            var content = GetResults(q, dataTableParam, dataTableOptions, properties.Select(p => Tuple.Create(p.Name, (string)null, p.PropertyType)).ToArray());
            this.Data = content;
            this.JsonRequestBehavior = JsonRequestBehavior.DenyGet;
        }

        private static readonly List<PropertyTransformer> PropertyTransformers = new List<PropertyTransformer>()
        {
            Guard<DateTimeOffset>(dateTimeOffset => dateTimeOffset.ToLocalTime().ToString("g")),
            Guard<DateTime>(dateTime => dateTime.ToLocalTime().ToString("g")),
            Guard<IHtmlString>(s => s.ToHtmlString()),
            Guard<object>(o => (o ?? "").ToString())
        };

        public delegate object PropertyTransformer(Type type, object value);

        public delegate object GuardedValueTransformer<TVal>(TVal value);

        private static PropertyTransformer Guard<TVal>(GuardedValueTransformer<TVal> transformer)
        {
            return (t, v) =>
            {
                if (!typeof(TVal).IsAssignableFrom(t))
                {
                    return null;
                }
                return transformer((TVal)v);
            };
        }

        public static void RegisterFilter<TVal>(GuardedValueTransformer<TVal> filter)
        {
            PropertyTransformers.Add(Guard<TVal>(filter));
        }

        private DataTablesData GetResults(IQueryable<T> data, DataTablesParam param, DataTablesOptions dataTableOptions, Tuple<string, string, Type>[] searchColumns)
        {
            int totalRecords = data.Count();

            int totalRecordsDisplay;
            var dtParameters = param;
            var columns = searchColumns;

            string sortString = "";
            for (int i = 0; i < dtParameters.iSortingCols; i++)
            {
                int columnNumber = dtParameters.iSortCol[i];
                string columnName = columns[columnNumber].Item1;
                string sortDir = dtParameters.sSortDir[i];
                if (i != 0)
                    sortString += ", ";
                if (dataTableOptions.SearchAliases.ContainsKey(columnName))
                {
                    var toks = dataTableOptions.SearchAliases[columnName].Split(',');
                    for (var xx = 0; xx < toks.Length; xx++)
                    {
                        if (xx == (toks.Length - 1))
                        {
                            sortString += toks[xx] + " " + sortDir;
                        }
                        else
                        {
                            sortString += toks[xx] + " " + sortDir + ", ";
                        }
                    }
                }
                else
                {
                    sortString += columnName + " " + sortDir;
                }
            }

            totalRecordsDisplay = data.Count();

            data = data.OrderBy(sortString);
            data = data.Skip(dtParameters.iDisplayStart);
            if (dtParameters.iDisplayLength > -1)
            {
                data = data.Take(dtParameters.iDisplayLength);
            }

            var type = typeof(TRes);
            var properties = type.GetProperties();

            var aaData = data.Select(_transform).Cast<object>().ToArray();

            var result = new DataTablesData
            {
                iTotalRecords = totalRecords,
                iTotalDisplayRecords = totalRecordsDisplay,
                sEcho = param.sEcho,
                aaData = aaData
            };

            return result;
        }

        private object GetTransformedValue(Type propertyType, object value)
        {
            foreach (var transformer in PropertyTransformers)
            {
                var result = transformer(propertyType, value);
                if (result != null) return result;
            }
            return (value as object ?? "").ToString();
        }
    }
}